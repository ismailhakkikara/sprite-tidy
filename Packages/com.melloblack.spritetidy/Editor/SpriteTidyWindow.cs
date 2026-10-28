using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MelloBlack.SpriteTidy.Core;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace MelloBlack.SpriteTidy
{
    public sealed class SpriteTidyWindow : EditorWindow
    {
        const float SidebarWidth = 340f;
        const float ThumbnailSize = 128f;
        const double PreviewDelay = 0.3;

        [SerializeField] List<Texture2D> inputs = new List<Texture2D>();
        [SerializeField] SpriteTidyPreset preset;
        [SerializeField] SpriteTidyPreset defaultPreset;
        [SerializeField] string outputFolder = "";
        [SerializeField] int selectedIndex = -1;

        SerializedObject presetObject;
        Vector2 inputScroll, settingsScroll, spriteScroll;
        GUIStyle inputStyle;
        Texture2D checker;

        Texture2D loadedTexture;
        RgbaImage loadedImage;
        Texture2D cleanedPreview;
        readonly List<Texture2D> spritePreviews = new List<Texture2D>();
        string previewInfo;
        bool previewDirty;
        double previewDueTime;
        int presetDirtyCount = -1;

        [MenuItem("Window/Sprite Tidy")]
        public static SpriteTidyWindow Open()
        {
            var window = GetWindow<SpriteTidyWindow>();
            window.titleContent = new GUIContent("Sprite Tidy");
            window.minSize = new Vector2(760, 460);
            return window;
        }

        [MenuItem("Assets/Sprite Tidy/Open in Sprite Tidy", false, 1100)]
        static void OpenWithSelection() => Open().Add(Collect(Selection.GetFiltered<Object>(SelectionMode.Assets)));

        [MenuItem("Assets/Sprite Tidy/Open in Sprite Tidy", true)]
        static bool CanOpenWithSelection() => Collect(Selection.GetFiltered<Object>(SelectionMode.Assets)).Any();

        /// <summary>Adds textures to the list, skipping ones already in it.</summary>
        public void Add(IEnumerable<Texture2D> textures)
        {
            int before = inputs.Count;
            foreach (var texture in textures)
                if (texture != null && !inputs.Contains(texture)) inputs.Add(texture);

            if (inputs.Count > before && (selectedIndex < 0 || selectedIndex >= inputs.Count)) Select(before);
            Repaint();
        }

        Texture2D SelectedTexture => selectedIndex >= 0 && selectedIndex < inputs.Count ? inputs[selectedIndex] : null;

        SpriteTidyPreset ActivePreset
        {
            get
            {
                if (preset != null) return preset;
                if (defaultPreset == null)
                {
                    defaultPreset = CreateInstance<SpriteTidyPreset>();
                    defaultPreset.name = "Default";
                    defaultPreset.hideFlags = HideFlags.HideAndDontSave;
                }

                return defaultPreset;
            }
        }

        Texture2D Checker
        {
            get
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

        void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            MarkPreviewDirty(0);
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            DestroyPreviews();
            if (checker != null) DestroyImmediate(checker);
        }

        void OnDestroy()
        {
            if (defaultPreset != null) DestroyImmediate(defaultPreset);
        }

        void OnProjectChange()
        {
            // The source file may have been changed or reimported.
            loadedImage = null;
            MarkPreviewDirty(PreviewDelay);
        }

        void OnEditorUpdate()
        {
            // Catches edits made to the preset asset in the Inspector as well as in this window.
            int dirtyCount = EditorUtility.GetDirtyCount(ActivePreset);
            if (dirtyCount != presetDirtyCount)
            {
                presetDirtyCount = dirtyCount;
                MarkPreviewDirty(PreviewDelay);
            }

            if (previewDirty && EditorApplication.timeSinceStartup >= previewDueTime)
            {
                previewDirty = false;
                RebuildPreview();
            }
        }

        void MarkPreviewDirty(double delay)
        {
            previewDirty = true;
            previewDueTime = EditorApplication.timeSinceStartup + delay;
        }

        void Select(int index)
        {
            selectedIndex = index;
            MarkPreviewDirty(0);
        }

        void OnGUI()
        {
            if (inputStyle == null) inputStyle = new GUIStyle(EditorStyles.miniButtonLeft) { alignment = TextAnchor.MiddleLeft };

            HandleDragAndDrop();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth)))
                {
                    DrawInputs();
                    DrawSettings();
                    DrawOutput();
                }

                DrawPreview();
            }
        }

        void DrawInputs()
        {
            EditorGUILayout.LabelField("Images", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Selected")) Add(Collect(Selection.GetFiltered<Object>(SelectionMode.Assets)));
                using (new EditorGUI.DisabledScope(inputs.Count == 0))
                {
                    if (GUILayout.Button("Clear"))
                    {
                        inputs.Clear();
                        Select(-1);
                    }
                }
            }

            inputScroll = EditorGUILayout.BeginScrollView(inputScroll, GUILayout.Height(140));
            if (inputs.Count == 0)
                EditorGUILayout.HelpBox("Drag images or folders here, or select them in the Project window and press Add Selected.", MessageType.None);

            for (int i = 0; i < inputs.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string label = inputs[i] != null ? inputs[i].name : "(missing)";
                    if (GUILayout.Toggle(i == selectedIndex, label, inputStyle) && i != selectedIndex) Select(i);
                    if (GUILayout.Button("×", EditorStyles.miniButtonRight, GUILayout.Width(22)))
                    {
                        inputs.RemoveAt(i);
                        Select(selectedIndex > i ? selectedIndex - 1 : Math.Min(selectedIndex, inputs.Count - 1));
                        GUIUtility.ExitGUI();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var chosen = (SpriteTidyPreset)EditorGUILayout.ObjectField("Preset", preset, typeof(SpriteTidyPreset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    preset = chosen;
                    MarkPreviewDirty(0);
                }

                if (preset == null && GUILayout.Button(new GUIContent("Save As…", "Save these settings as a preset asset."), GUILayout.Width(72)))
                    SaveDefaultAsPreset();
            }

            if (presetObject == null || presetObject.targetObject != ActivePreset) presetObject = new SerializedObject(ActivePreset);

            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 150;
            presetObject.Update();
            EditorGUI.BeginChangeCheck();
            var property = presetObject.GetIterator();
            property.NextVisible(true); // m_Script
            while (property.NextVisible(false)) EditorGUILayout.PropertyField(property, true);
            if (EditorGUI.EndChangeCheck())
            {
                presetObject.ApplyModifiedProperties();
                MarkPreviewDirty(PreviewDelay);
            }

            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUILayout.EndScrollView();
        }

        void SaveDefaultAsPreset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Sprite Tidy Preset", "SpriteTidyPreset", "asset", "Choose where to save the preset.");
            if (string.IsNullOrEmpty(path)) return;

            var asset = Instantiate(ActivePreset);
            asset.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            preset = asset;
            MarkPreviewDirty(0);
            GUIUtility.ExitGUI();
        }

        void DrawOutput()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                outputFolder = EditorGUILayout.TextField(
                    new GUIContent("Folder", "Project folder for the results. Leave empty to put them in a \"Tidy\" folder next to each source image."),
                    outputFolder);
                if (GUILayout.Button("…", GUILayout.Width(24))) BrowseOutputFolder();
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
                EditorGUILayout.LabelField($"Results go to a \"{SpriteTidyProcessor.DefaultSubfolder}\" folder next to each image.", EditorStyles.miniLabel);

            int count = inputs.Count(t => t != null);
            using (new EditorGUI.DisabledScope(count == 0))
            {
                if (GUILayout.Button(count == 1 ? "Process 1 Image" : $"Process {count} Images", GUILayout.Height(28)))
                    ProcessAll();
            }

            EditorGUILayout.Space();
        }

        void BrowseOutputFolder()
        {
            string chosen = EditorUtility.OpenFolderPanel("Sprite Tidy Output Folder", "Assets", "");
            if (string.IsNullOrEmpty(chosen)) return;

            string assets = Application.dataPath.Replace('\\', '/');
            chosen = chosen.Replace('\\', '/');
            if (chosen == assets || chosen.StartsWith(assets + "/", StringComparison.OrdinalIgnoreCase))
                outputFolder = "Assets" + chosen.Substring(assets.Length);
            else
                EditorUtility.DisplayDialog("Sprite Tidy", "Choose a folder inside this project's Assets folder.", "OK");
        }

        void ProcessAll()
        {
            var written = SpriteTidyProcessor.Process(inputs.Where(t => t != null).ToList(), ActivePreset, outputFolder);
            if (written.Count > 0) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(written[0]));
            ShowNotification(new GUIContent(written.Count == 1 ? "1 sprite written" : $"{written.Count} sprites written"));
            GUIUtility.ExitGUI();
        }

        void DrawPreview()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (SelectedTexture == null)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Select an image to preview it.", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                    return;
                }

                EditorGUILayout.LabelField(previewInfo ?? "Processing…", EditorStyles.wordWrappedMiniLabel);

                float topHeight = Mathf.Max(140f, (position.height - 80f) * 0.45f);
                var top = GUILayoutUtility.GetRect(0, topHeight, GUILayout.ExpandWidth(true));
                float half = (top.width - 8f) / 2f;
                var original = new Rect(top.x, top.y, half, top.height);
                var cleaned = new Rect(top.x + half + 8f, top.y, half, top.height);
                DrawLabeledImage(original, "Original", SelectedTexture);
                DrawLabeledImage(cleaned, "Cleaned", cleanedPreview);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Sprites ({spritePreviews.Count})", EditorStyles.miniBoldLabel);
                spriteScroll = EditorGUILayout.BeginScrollView(spriteScroll);
                DrawSpriteGrid();
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawSpriteGrid()
        {
            float width = position.width - SidebarWidth - 24f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(width / (ThumbnailSize + 8f)));
            for (int start = 0; start < spritePreviews.Count; start += columns)
            {
                var row = GUILayoutUtility.GetRect(width, ThumbnailSize + 18f);
                for (int c = 0; c < columns && start + c < spritePreviews.Count; c++)
                {
                    var cell = new Rect(row.x + c * (ThumbnailSize + 8f), row.y, ThumbnailSize, ThumbnailSize + 18f);
                    DrawLabeledImage(cell, (start + c + 1).ToString(), spritePreviews[start + c]);
                }
            }
        }

        void DrawLabeledImage(Rect rect, string label, Texture texture)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 16f), label, EditorStyles.miniLabel);
            if (texture == null) return;

            var area = new Rect(rect.x, rect.y + 18f, rect.width, rect.height - 18f);
            float scale = Mathf.Min(area.width / texture.width, area.height / texture.height);
            var fitted = new Rect(0, 0, texture.width * scale, texture.height * scale) { center = area.center };
            GUI.DrawTextureWithTexCoords(fitted, Checker, new Rect(0, 0, fitted.width / 16f, fitted.height / 16f));
            GUI.DrawTexture(fitted, texture, ScaleMode.StretchToFill, true);
        }

        void RebuildPreview()
        {
            DestroyPreviews();
            var texture = SelectedTexture;
            if (texture == null)
            {
                previewInfo = null;
                Repaint();
                return;
            }

            try
            {
                if (loadedImage == null || loadedTexture != texture)
                {
                    loadedImage = SpriteTidyIO.Load(AssetDatabase.GetAssetPath(texture));
                    loadedTexture = texture;
                }

                var stopwatch = Stopwatch.StartNew();
                var result = TidyPipeline.Process(loadedImage, ActivePreset.ToSettings());
                stopwatch.Stop();

                cleanedPreview = SpriteTidyIO.ToTexture(result.Cleaned);
                foreach (var sprite in result.Sprites) spritePreviews.Add(SpriteTidyIO.ToTexture(sprite.Image));

                string background = result.Background != null ? result.Background.ToString() : "not removed";
                previewInfo = $"{loadedImage.Width}×{loadedImage.Height}  ·  background: {background}  ·  " +
                              $"{result.Sprites.Count} sprite(s)  ·  {stopwatch.ElapsedMilliseconds} ms";
            }
            catch (Exception e)
            {
                previewInfo = $"Preview failed: {e.Message}";
                Debug.LogException(e);
            }

            Repaint();
        }

        void DestroyPreviews()
        {
            if (cleanedPreview != null) DestroyImmediate(cleanedPreview);
            cleanedPreview = null;
            foreach (var texture in spritePreviews)
                if (texture != null) DestroyImmediate(texture);
            spritePreviews.Clear();
        }

        void HandleDragAndDrop()
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;

            var textures = Collect(DragAndDrop.objectReferences).ToList();
            if (textures.Count == 0) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                Add(textures);
            }

            evt.Use();
        }

        /// <summary>Textures among the given assets, including those inside folders but not Sprite Tidy's own output folders.</summary>
        static IEnumerable<Texture2D> Collect(IEnumerable<Object> objects)
        {
            foreach (var obj in objects)
            {
                if (obj is Texture2D texture)
                {
                    yield return texture;
                    continue;
                }

                if (obj is Sprite sprite)
                {
                    if (sprite.texture != null) yield return sprite.texture;
                    continue;
                }

                string folder = AssetDatabase.GetAssetPath(obj);
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                string outputMarker = "/" + SpriteTidyProcessor.DefaultSubfolder + "/";
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.Contains(outputMarker)) continue;
                    var found = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (found != null) yield return found;
                }
            }
        }
    }
}
