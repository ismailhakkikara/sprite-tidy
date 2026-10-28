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
        static readonly Color PivotColor = new Color(1f, 0.25f, 0.25f);
        static readonly Color CellColor = new Color(1f, 1f, 1f, 0.35f);

        [SerializeField] Texture2D image;
        [SerializeField] FixSettings settings = new FixSettings();

        CleanImage clean;
        Texture2D cleanFor;
        IReadOnlyList<OutputImage> result;
        readonly List<Texture2D> previews = new List<Texture2D>();
        string message;
        Texture2D checker;

        [MenuItem("Window/Sprite Tidy")]
        public static SpriteTidyWindow Open()
        {
            var window = GetWindow<SpriteTidyWindow>();
            window.titleContent = new GUIContent("Sprite Tidy");
            window.minSize = new Vector2(640, 420);
            return window;
        }

        [MenuItem("Assets/Fix with Sprite Tidy", false, 1100)]
        static void OpenWithSelection() => Open().SetImage(Selection.activeObject as Texture2D);

        [MenuItem("Assets/Fix with Sprite Tidy", true)]
        static bool CanOpenWithSelection() => Selection.activeObject is Texture2D;

        public void SetImage(Texture2D texture)
        {
            image = texture;
            clean = null;
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

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(PanelWidth)))
                {
                    EditorGUILayout.Space();
                    DrawSettings();
                }

                DrawPreview();
            }
        }

        void DrawSettings()
        {
            EditorGUI.BeginChangeCheck();
            var picked = (Texture2D)EditorGUILayout.ObjectField(
                new GUIContent("Image", "The picture to fix. Drag it here from the Project window."), image, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck()) SetImage(picked);

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
            settings.Pivot = PivotGrid(settings.Pivot);
            if (EditorGUI.EndChangeCheck()) Arrange();

            EditorGUILayout.Space();
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.None);

            using (new EditorGUI.DisabledScope(!CanSave()))
            {
                if (GUILayout.Button("Fix", GUILayout.Height(32))) Save();
            }
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

        void DrawPreview()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (image == null)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Put an image in the Image field to start.", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                    return;
                }

                // Side by side or one above the other, whichever shows the images bigger.
                const float gap = 8f;
                var area = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                var resultTexture = previews.Count == 1 ? previews[0] : null;
                float sideBySideScale = ShownScale(resultTexture, (area.width - gap) / 2f, area.height);
                float stackedScale = ShownScale(resultTexture, area.width, (area.height - gap) / 2f);

                Rect first, second;
                if (sideBySideScale >= stackedScale)
                {
                    float half = (area.width - gap) / 2f;
                    first = new Rect(area.x, area.y, half, area.height);
                    second = new Rect(area.x + half + gap, area.y, half, area.height);
                }
                else
                {
                    float half = (area.height - gap) / 2f;
                    first = new Rect(area.x, area.y, area.width, half);
                    second = new Rect(area.x, area.y + half + gap, area.width, half);
                }

                DrawImage(first, "Original", image, null);
                if (previews.Count == 1) DrawImage(second, "Result", previews[0], result[0].Sprites);
                else if (previews.Count > 1) DrawGrid(second);
            }
        }

        /// <summary>How big the original and the result would be shown in two spaces of this size (the smaller of the two).</summary>
        float ShownScale(Texture resultTexture, float width, float height)
        {
            height -= 18f; // the label above each image
            float Fit(Texture texture) => texture == null ? float.MaxValue : Mathf.Min(width / texture.width, height / texture.height);
            return Mathf.Min(Fit(image), Fit(resultTexture));
        }

        /// <summary>Several separate files: one small preview each.</summary>
        void DrawGrid(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 16f), "Result", EditorStyles.miniLabel);
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

        static void DrawOutline(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), color);
        }

        /// <summary>Runs the slow clean-up step when the image changes, then lays out the result.</summary>
        void Refresh()
        {
            if (image == null)
            {
                clean = null;
                Arrange();
                return;
            }

            if (clean == null || cleanFor != image)
            {
                try
                {
                    clean = SpriteFixer.Clean(SpriteTidyIO.Load(AssetDatabase.GetAssetPath(image)));
                    cleanFor = image;
                }
                catch (Exception e)
                {
                    clean = null;
                    ClearPreviews();
                    result = null;
                    message = "This image could not be read: " + e.Message;
                    Repaint();
                    return;
                }
            }

            Arrange();
        }

        /// <summary>The fast step: scales and places the objects with the current settings.</summary>
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
            if (clean == null) return null;

            int count = clean.Parts.Count;
            if (count == 0) return "No objects found in this image.";

            string background = clean.Background == BackgroundType.Solid ? "a plain"
                : clean.Background == BackgroundType.Checkerboard ? "a checkerboard"
                : "a transparent";
            string found = count == 1 ? $"Found 1 object on {background} background." : $"Found {count} objects on {background} background.";

            string sourcePath = AssetDatabase.GetAssetPath(image);
            if (!sourcePath.StartsWith("Assets/", StringComparison.Ordinal))
                return found + "\nThe image must be in the Assets folder to save next to it.";

            var files = SpriteTidySaver.OutputPaths(sourcePath, result.Count).Select(Path.GetFileName).ToList();
            int size = settings.Size;
            string saves;
            if (settings.Mode == ImageMode.Single)
                saves = $"Saves {files[0]}: one sprite of {size} × {size}.";
            else if (settings.Output == OutputType.Separate)
                saves = $"Saves {files.Count} files of {size} × {size}: {files[0]} to {files[files.Count - 1]}.";
            else
                saves = $"Saves {files[0]}: {count} sprites of {size} × {size} in a {result[0].Image.Width / size} × {result[0].Image.Height / size} grid.";
            return found + "\n" + saves;
        }

        bool CanSave() => result != null && result.Count > 0 && AssetDatabase.GetAssetPath(image).StartsWith("Assets/", StringComparison.Ordinal);

        void Save()
        {
            var written = SpriteTidySaver.Save(AssetDatabase.GetAssetPath(image), result);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(written[0]));
            ShowNotification(new GUIContent(written.Count == 1 ? "Saved " + Path.GetFileName(written[0]) : $"Saved {written.Count} files"));
            GUIUtility.ExitGUI();
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
