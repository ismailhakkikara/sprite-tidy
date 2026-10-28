using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelloBlack.SpriteTidy.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MelloBlack.SpriteTidy
{
    public sealed class SpriteTidyWindow : EditorWindow
    {
        const float PanelWidth = 300f;
        const float Gap = 8f;
        const float Thumbnail = 56f;
        const float ThumbnailGap = 4f;
        const int PickerId = 0x5717;
        const string DontAskBeforeOverwrite = "MelloBlack.SpriteTidy.DontAskBeforeOverwrite";
        static readonly Color PivotColor = new Color(1f, 0.25f, 0.25f);
        static readonly Color CellColor = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color ShownColor = new Color(0.25f, 0.6f, 1f);

        [SerializeField] List<Texture2D> images = new List<Texture2D>();
        [SerializeField] int shown;
        [SerializeField] FixSettings settings = new FixSettings();

        // The slow clean-up step, done once for each image.
        readonly Dictionary<Texture2D, CleanImage> cleaned = new Dictionary<Texture2D, CleanImage>();

        // The image whose result is shown.
        CleanImage clean;
        string error;
        IReadOnlyList<OutputImage> result;
        readonly List<Texture2D> previews = new List<Texture2D>();
        string message;

        Texture2D checker;
        GUIStyle removeStyle;
        GUIStyle hintStyle;

        // The result is shown to the right of the settings, or below them when that shows it bigger.
        bool resultBelow;

        // Height of the settings, measured on the last repaint.
        float settingsHeight = 260f;

        [MenuItem("Window/Sprite Tidy")]
        public static SpriteTidyWindow Open()
        {
            var window = GetWindow<SpriteTidyWindow>();
            window.titleContent = new GUIContent("Sprite Tidy");
            window.minSize = new Vector2(320, 360);
            return window;
        }

        [MenuItem("Assets/Fix with Sprite Tidy", false, 1100)]
        static void OpenWithSelection() => Open().SetImages(Selection.objects.Select(TextureOf));

        [MenuItem("Assets/Fix with Sprite Tidy", true)]
        static bool CanOpenWithSelection() => Selection.objects.Any(o => TextureOf(o) != null);

        /// <summary>The image file an asset is in: the texture itself, or the texture a sprite is cut from.</summary>
        static Texture2D TextureOf(Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path) as Texture2D;
        }

        /// <summary>Shows these images in the window, in place of the ones there before.</summary>
        public void SetImages(IEnumerable<Texture2D> textures)
        {
            images.Clear();
            AddImages(textures);
        }

        /// <summary>Adds the images that are not in the window yet, and shows the result of the first one.</summary>
        public void AddImages(IEnumerable<Texture2D> textures)
        {
            var added = textures.Where(t => t != null && !images.Contains(t)).Distinct().ToList();
            if (added.Count > 0) shown = images.Count;
            images.AddRange(added);
            Refresh();
        }

        void RemoveImage(int index)
        {
            images.RemoveAt(index);
            if (shown > index) shown--;
            Refresh();
        }

        void OnEnable()
        {
            // A window saved by an older version may still have the removed Atlas output selected.
            if (!Enum.IsDefined(typeof(OutputType), settings.Output)) settings.Output = OutputType.Sheet;
            Refresh();
        }

        void OnDisable()
        {
            ClearPreviews();
            if (checker != null) DestroyImmediate(checker);
        }

        void OnProjectChange()
        {
            // An image may have been deleted.
            if (images.Any(i => i == null)) Refresh();
            else Repaint();
        }

        void OnGUI()
        {
            // Decide once per frame, in the Layout event, so all events of the frame use the same layout.
            if (Event.current.type == EventType.Layout) resultBelow = ResultFitsBetterBelow();

            if (resultBelow)
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawSettings(position.width);
                    DrawResult();
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(PanelWidth))) DrawSettings(PanelWidth);
                    DrawResult();
                }
            }

            AcceptDroppedImages();
            AcceptPickedImage();
        }

        /// <summary>Whether the result shows bigger below the settings than to their right.</summary>
        bool ResultFitsBetterBelow()
        {
            var texture = previews.Count == 1 ? previews[0] : null;
            float aspect = texture != null ? (float)texture.width / texture.height : 1f;
            float right = ShownHeight(aspect, position.width - PanelWidth - Gap, position.height - Gap);
            float below = ShownHeight(aspect, position.width - Gap, position.height - settingsHeight - Gap);
            return below > right;
        }

        /// <summary>How tall an image of this aspect is shown in a space of this size, under its label.</summary>
        static float ShownHeight(float aspect, float width, float height)
        {
            height -= 18f;
            return width <= 0 || height <= 0 ? 0 : Mathf.Min(width / aspect, height);
        }

        void DrawSettings(float width)
        {
            EditorGUILayout.Space();
            DrawImages(width);

            EditorGUI.BeginChangeCheck();
            settings.Mode = (ImageMode)Choice("Mode",
                "Single: the picture shows one object.\nMultiple: it shows several objects, like a sprite sheet.",
                (int)settings.Mode, "Single", "Multiple");

            if (settings.Mode == ImageMode.Multiple)
            {
                settings.Output = (OutputType)Choice("Output",
                    "Sheet: one texture, objects in a grid of equal cells. For animations: frames are lined up, so they don't move.\n" +
                    "Separate: one file for each object. To pack them for performance, add them to a Unity Sprite Atlas.",
                    (int)settings.Output, "Sheet", "Separate");

                if (settings.Output == OutputType.Sheet)
                {
                    settings.LineUp = (LineUp)Choice("Line up",
                        "Which part of every frame stays in the same place.\n" +
                        "Bottom: the base, for things that stand on the ground.\n" +
                        "Center: the middle, for things that spin or grow from their middle.\n" +
                        "Top: the top, when effects appear below the object.",
                        (int)settings.LineUp, "Bottom", "Center", "Top");
                }
            }

            settings.Size = Mathf.Clamp(EditorGUILayout.DelayedIntField(
                new GUIContent("Size", "Width and height of each sprite, in pixels."), settings.Size), 16, 4096);
            settings.Margin = Mathf.Clamp(EditorGUILayout.DelayedIntField(
                new GUIContent("Margin", "Empty pixels between the object and the edges of its sprite."), settings.Margin), 0, settings.Size / 2 - 1);
            settings.Pivot = PivotGrid(settings.Pivot);
            if (EditorGUI.EndChangeCheck()) Arrange();

            EditorGUILayout.Space();
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.None);

            bool canSave = CanSave();
            string noOverwrite = WhyNoOverwrite();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canSave))
                {
                    if (GUILayout.Button(new GUIContent("Save as New", SaveAsNewTooltip()), GUILayout.Height(32))) Save(false);
                }

                using (new EditorGUI.DisabledScope(!canSave || noOverwrite != null))
                {
                    if (GUILayout.Button(new GUIContent("Overwrite", noOverwrite ?? OverwriteTooltip()), GUILayout.Height(32))) Save(true);
                }
            }

            if (Event.current.type == EventType.Repaint) settingsHeight = GUILayoutUtility.GetLastRect().yMax;
        }

        /// <summary>The images to fix, as thumbnails: click one to see its result.</summary>
        void DrawImages(float width)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(images.Count > 1 ? $"Images ({images.Count})" : "Images",
                    "The pictures to fix. Drag them into this window from the Project window, or select them there and " +
                    "choose Fix with Sprite Tidy from the right-click menu.\nClick a picture to see its result."));
                if (images.Count > 1 && GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(48)))
                    SetImages(Array.Empty<Texture2D>());
            }

            // The last cell is the button that adds an image.
            int columns = Mathf.Max(1, Mathf.FloorToInt((width - ThumbnailGap) / (Thumbnail + ThumbnailGap)));
            int rows = images.Count / columns + 1;
            var box = GUILayoutUtility.GetRect(0, rows * (Thumbnail + ThumbnailGap) + ThumbnailGap, GUILayout.ExpandWidth(true));
            GUI.Box(box, GUIContent.none, EditorStyles.helpBox);

            for (int i = 0; i <= images.Count; i++)
            {
                var cell = new Rect(
                    box.x + ThumbnailGap + i % columns * (Thumbnail + ThumbnailGap),
                    box.y + ThumbnailGap + i / columns * (Thumbnail + ThumbnailGap),
                    Thumbnail, Thumbnail);

                if (i == images.Count) DrawAddButton(cell, box);
                else if (DrawThumbnail(cell, i)) break;
            }

            EditorGUILayout.Space();
        }

        /// <summary>Draws one image of the list. Returns true when it was removed.</summary>
        bool DrawThumbnail(Rect cell, int index)
        {
            var image = images[index];
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawTextureTransparent(cell, image, ScaleMode.ScaleToFit);
                if (index == shown && images.Count > 1) DrawOutline(cell, ShownColor, 2f);
            }

            removeStyle = removeStyle ?? new GUIStyle(EditorStyles.miniButton)
            {
                padding = new RectOffset(), margin = new RectOffset(), fixedHeight = 16f, alignment = TextAnchor.MiddleCenter,
            };
            if (GUI.Button(new Rect(cell.xMax - 16f, cell.y, 16f, 16f), new GUIContent("×", "Remove from the list"), removeStyle))
            {
                RemoveImage(index);
                return true;
            }

            if (GUI.Button(cell, new GUIContent("", FileName(image)), GUIStyle.none) && index != shown)
            {
                shown = index;
                Refresh();
            }

            return false;
        }

        void DrawAddButton(Rect cell, Rect box)
        {
            if (GUI.Button(cell, new GUIContent("+", "Pick an image")))
                EditorGUIUtility.ShowObjectPicker<Texture2D>(null, false, "", PickerId);

            if (images.Count > 0) return;
            hintStyle = hintStyle ?? new GUIStyle(EditorStyles.wordWrappedMiniLabel) { alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(cell.xMax + Gap, cell.y, box.xMax - cell.xMax - 2 * Gap, cell.height),
                "Drag images here, or press + to pick one.", hintStyle);
        }

        /// <summary>Images dropped anywhere on the window are added to the list.</summary>
        void AcceptDroppedImages()
        {
            var e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;

            var dropped = DragAndDrop.objectReferences.Select(TextureOf).Where(t => t != null).ToList();
            if (dropped.Count == 0) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddImages(dropped);
            }

            e.Use();
        }

        void AcceptPickedImage()
        {
            var e = Event.current;
            if (e.commandName != "ObjectSelectorClosed" || EditorGUIUtility.GetObjectPickerControlID() != PickerId) return;

            if (EditorGUIUtility.GetObjectPickerObject() is Texture2D picked) AddImages(new[] { picked });
            e.Use();
        }

        static int Choice(string label, string tooltip, int selected, params string[] options)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));
                return GUILayout.Toolbar(selected, options);
            }
        }

        static Pivot PivotGrid(Pivot current)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Pivot",
                    "Where the object sits inside its sprite. The sprite pivot is set to the same point.\n" +
                    "Bottom: objects stand on the bottom edge, centered on their base.\n" +
                    "In a Sheet, the whole animation is placed this way."));

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(84)))
                {
                    for (int row = 0; row < 3; row++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            for (int column = 0; column < 3; column++)
                            {
                                var pivot = (Pivot)(row * 3 + column);
                                var content = new GUIContent("", ObjectNames.NicifyVariableName(pivot.ToString()));
                                if (GUILayout.Toggle(pivot == current, content, "Button", GUILayout.Width(26), GUILayout.Height(20)))
                                    current = pivot;
                            }
                        }
                    }
                }

                EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(current.ToString()), EditorStyles.miniLabel);
            }

            return current;
        }

        void DrawResult()
        {
            var area = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (images.Count == 0)
            {
                GUI.Label(area, "Drag images into this window to start.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            area = new Rect(area.x + Gap / 2f, area.y + Gap / 2f, area.width - Gap, area.height - Gap);
            string label = images.Count > 1 ? "Result: " + FileName(ShownImage) : "Result";
            if (previews.Count == 1) DrawImage(area, label, previews[0], result[0].Sprites);
            else if (previews.Count > 1) DrawGrid(area, label);
        }

        /// <summary>Several separate files: one small preview each.</summary>
        void DrawGrid(Rect area, string label)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 16f), label, EditorStyles.miniLabel);
            area.yMin += 18f;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(previews.Count * area.width / Mathf.Max(1f, area.height)));
            int rows = Mathf.CeilToInt((float)previews.Count / columns);
            float cell = Mathf.Min(area.width / columns, area.height / rows);
            for (int i = 0; i < previews.Count; i++)
            {
                var rect = new Rect(area.x + i % columns * cell, area.y + i / columns * cell, cell - 4f, cell - 4f);
                DrawFitted(rect, previews[i], result[i].Sprites);
            }
        }

        void DrawImage(Rect area, string label, Texture texture, IReadOnlyList<SpriteArea> sprites)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 16f), label, EditorStyles.miniLabel);
            area.yMin += 18f;
            DrawFitted(area, texture, sprites);
        }

        /// <summary>Draws a texture over a checkerboard, with the sprite outlines and pivots marked.</summary>
        void DrawFitted(Rect area, Texture texture, IReadOnlyList<SpriteArea> sprites)
        {
            float scale = Mathf.Min(area.width / texture.width, area.height / texture.height);
            var rect = new Rect(0, 0, texture.width * scale, texture.height * scale) { center = area.center };
            GUI.DrawTextureWithTexCoords(rect, Checker(), new Rect(0, 0, rect.width / 16f, rect.height / 16f));
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            if (sprites == null) return;

            foreach (var sprite in sprites)
            {
                // Texture y counts from the bottom; GUI y counts from the top.
                var r = sprite.Rect;
                var outline = new Rect(rect.x + r.X * scale, rect.yMax - r.YMax * scale, r.Width * scale, r.Height * scale);
                if (sprites.Count > 1) DrawOutline(outline, CellColor);
                var pivot = new Vector2(outline.x + sprite.PivotX * outline.width, outline.yMax - sprite.PivotY * outline.height);
                EditorGUI.DrawRect(new Rect(pivot.x - 2f, pivot.y - 2f, 5f, 5f), PivotColor);
            }
        }

        static void DrawOutline(Rect r, Color color, float width = 1f)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, width), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - width, r.width, width), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, width, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - width, r.y, width, r.height), color);
        }

        Texture2D ShownImage => shown < images.Count ? images[shown] : null;

        static string FileName(Object asset) => Path.GetFileName(AssetDatabase.GetAssetPath(asset));

        static bool InAssets(Object asset) => AssetDatabase.GetAssetPath(asset).StartsWith("Assets/", StringComparison.Ordinal);

        /// <summary>The slow clean-up step, done once for each image. Throws when the image can't be read.</summary>
        CleanImage CleanOf(Texture2D image)
        {
            if (!cleaned.TryGetValue(image, out var found))
                cleaned[image] = found = SpriteFixer.Clean(SpriteTidyIO.Load(AssetDatabase.GetAssetPath(image)));
            return found;
        }

        /// <summary>Cleans up the shown image if that wasn't done yet, then lays out its result.</summary>
        void Refresh()
        {
            images.RemoveAll(i => i == null);
            foreach (var gone in cleaned.Keys.Where(k => !images.Contains(k)).ToList()) cleaned.Remove(gone);
            shown = Mathf.Clamp(shown, 0, Mathf.Max(0, images.Count - 1));

            clean = null;
            error = null;
            if (ShownImage != null)
            {
                try
                {
                    clean = CleanOf(ShownImage);
                }
                catch (Exception e)
                {
                    error = e.Message;
                }
            }

            Arrange();
        }

        /// <summary>The fast step: scales and places the objects of the shown image with the current settings.</summary>
        void Arrange()
        {
            ClearPreviews();
            result = clean != null ? SpriteFixer.Arrange(clean, settings) : null;
            if (result != null)
                foreach (var output in result) previews.Add(SpriteTidyIO.ToTexture(output.Image));
            message = Describe();
            Repaint();
        }

        string Describe()
        {
            var image = ShownImage;
            if (image == null) return null;

            // With several images, the message is about the one whose result is shown.
            string name = images.Count > 1 ? FileName(image) : null;
            if (clean == null) return $"{name ?? "This image"} could not be read: {error}";

            int count = clean.Parts.Count;
            if (count == 0) return name == null ? "No objects found in this image." : $"No objects found in {name}.";

            string background = clean.Background == BackgroundType.Solid ? "a plain"
                : clean.Background == BackgroundType.Checkerboard ? "a checkerboard"
                : "a transparent";
            string found = name == null
                ? $"Found {Count(count, "object")} on {background} background."
                : $"Found {Count(count, "object")} in {name}, on {background} background.";

            string size = $"{settings.Size} × {settings.Size}";
            string makes;
            if (settings.Mode == ImageMode.Single)
                makes = $"Makes one sprite of {size}.";
            else if (settings.Output == OutputType.Separate)
                makes = $"Makes {Count(count, "file")} of {size}.";
            else
                makes = $"Makes {Count(count, "sprite")} of {size} in a {result[0].Image.Width / settings.Size} × {result[0].Image.Height / settings.Size} grid.";

            var lines = new List<string> { found, makes };
            if (!InAssets(image)) lines.Add("The image must be in the Assets folder to save next to it.");
            else if (WhyNoOverwrite() is string noOverwrite) lines.Add(noOverwrite);
            return string.Join("\n", lines);
        }

        static string Count(int count, string thing) => count == 1 ? $"1 {thing}" : $"{count} {thing}s";

        string SaveAsNewTooltip()
        {
            if (images.Count > 1)
                return "Saves the results next to the images, with _fixed added to their names. The images themselves don't change.";
            if (result == null || result.Count == 0)
                return "Saves the result next to the image, with _fixed added to its name. The image itself doesn't change.";

            var files = SpriteTidySaver.OutputPaths(AssetDatabase.GetAssetPath(images[0]), result.Count).Select(Path.GetFileName).ToList();
            string names = files.Count == 1 ? files[0] : $"{files[0]} to {files[files.Count - 1]}";
            return $"Saves {names} next to the image. The image itself doesn't change.";
        }

        string OverwriteTooltip() => images.Count > 1
            ? "Replaces the images with their results. Their sprites keep their IDs, so scenes, prefabs and animations that use them keep working."
            : "Replaces the image with the result. Its sprites keep their IDs, so scenes, prefabs and animations that use them keep working.";

        /// <summary>Why the images can't be overwritten, or null when they can.</summary>
        string WhyNoOverwrite()
        {
            if (settings.Mode == ImageMode.Multiple && settings.Output == OutputType.Separate)
                return "Overwrite doesn't work with Separate, which makes several files from one image.";

            var other = images.FirstOrDefault(i => !SpriteTidySaver.CanOverwrite(AssetDatabase.GetAssetPath(i)));
            if (other == null) return null;
            return images.Count == 1
                ? "Overwrite only works with PNG images."
                : $"Overwrite only works with PNG images, and {FileName(other)} is not one.";
        }

        /// <summary>
        /// With one image, whether it can be saved. With several, each one is checked when saving,
        /// and the ones that can't be saved are skipped.
        /// </summary>
        bool CanSave() => images.Count > 1 || (images.Count == 1 && result != null && result.Count > 0 && InAssets(images[0]));

        /// <summary>Asks before overwriting, unless the user chose not to be asked again.</summary>
        bool ConfirmOverwrite()
        {
            if (EditorPrefs.GetBool(DontAskBeforeOverwrite)) return true;

            int answer = EditorUtility.DisplayDialogComplex(
                images.Count == 1 ? $"Overwrite {FileName(images[0])}?" : $"Overwrite {images.Count} images?",
                (images.Count == 1 ? "The image is replaced with the result. " : "The images are replaced with their results. ") +
                "The original can't be brought back, unless you use version control.",
                "Overwrite", "Cancel", "Don't Ask Again");

            // Don't Ask Again overwrites too.
            if (answer == 2) EditorPrefs.SetBool(DontAskBeforeOverwrite, true);
            return answer != 1;
        }

        void Save(bool overwrite)
        {
            if (overwrite && !ConfirmOverwrite()) return;

            var written = new List<string>();
            var skipped = new List<string>();
            try
            {
                for (int i = 0; i < images.Count; i++)
                {
                    if (images.Count > 1) EditorUtility.DisplayProgressBar("Sprite Tidy", FileName(images[i]), (float)i / images.Count);
                    string problem = SaveImage(images[i], overwrite, written);
                    if (problem != null) skipped.Add($"{FileName(images[i])}: {problem}.");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            foreach (string line in skipped) Debug.LogWarning("Sprite Tidy skipped " + line);
            if (written.Count > 0) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(written[0]));

            string done = (overwrite ? "Overwrote " : "Saved ") + (written.Count == 1 ? Path.GetFileName(written[0]) : $"{written.Count} files");
            if (skipped.Count > 0) done += $"\nSkipped {Count(skipped.Count, "image")}, see the Console";
            ShowNotification(new GUIContent(done));

            if (overwrite) Refresh();
            GUIUtility.ExitGUI();
        }

        /// <summary>Fixes one image and saves it. Returns why it was skipped, or null when it was saved.</summary>
        string SaveImage(Texture2D image, bool overwrite, List<string> written)
        {
            if (!InAssets(image)) return "it is not in the Assets folder";

            CleanImage cleanImage;
            try
            {
                cleanImage = CleanOf(image);
            }
            catch (Exception e)
            {
                return "it could not be read: " + e.Message.TrimEnd('.');
            }

            var outputs = SpriteFixer.Arrange(cleanImage, settings);
            if (outputs.Count == 0) return "no objects were found in it";

            string path = AssetDatabase.GetAssetPath(image);
            if (overwrite)
            {
                SpriteTidySaver.Overwrite(path, outputs[0]);
                cleaned.Remove(image);
                written.Add(path);
            }
            else
            {
                written.AddRange(SpriteTidySaver.Save(path, outputs));
            }

            return null;
        }

        void ClearPreviews()
        {
            foreach (var texture in previews)
                if (texture != null) DestroyImmediate(texture);
            previews.Clear();
        }

        Texture2D Checker()
        {
            if (checker != null) return checker;
            checker = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };
            var light = new Color32(204, 204, 204, 255);
            var dark = new Color32(160, 160, 160, 255);
            checker.SetPixels32(new[] { light, dark, dark, light });
            checker.Apply(false);
            return checker;
        }
    }
}
