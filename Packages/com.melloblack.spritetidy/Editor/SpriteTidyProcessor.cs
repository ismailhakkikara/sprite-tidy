using System;
using System.Collections.Generic;
using System.IO;
using MelloBlack.SpriteTidy.Core;
using UnityEditor;
using UnityEngine;

namespace MelloBlack.SpriteTidy
{
    /// <summary>Processes texture assets and writes the results as sprite assets.</summary>
    public static class SpriteTidyProcessor
    {
        /// <summary>Folder created next to each source image when no output folder is given.</summary>
        public const string DefaultSubfolder = "Tidy";

        const string FallbackOutputFolder = "Assets/Sprite Tidy Output";

        /// <summary>
        /// Processes each texture and writes its sprites as PNGs. Files from earlier runs with the same names are
        /// overwritten, keeping their GUIDs so existing references stay intact.
        /// </summary>
        /// <param name="outputFolder">Project folder for all results, or empty for a "Tidy" folder next to each source.</param>
        /// <returns>Asset paths of the written sprites.</returns>
        public static List<string> Process(IList<Texture2D> textures, SpriteTidyPreset preset, string outputFolder)
        {
            var settings = preset.ToSettings();
            var written = new List<string>();
            try
            {
                for (int t = 0; t < textures.Count; t++)
                {
                    string sourcePath = AssetDatabase.GetAssetPath(textures[t]);
                    if (string.IsNullOrEmpty(sourcePath)) continue;

                    if (EditorUtility.DisplayCancelableProgressBar("Sprite Tidy", sourcePath, (float)t / textures.Count))
                        break;

                    TidyResult result;
                    try
                    {
                        result = TidyPipeline.Process(SpriteTidyIO.Load(sourcePath), settings);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Sprite Tidy could not process '{sourcePath}': {e.Message}", textures[t]);
                        continue;
                    }

                    if (result.Sprites.Count == 0)
                    {
                        Debug.LogWarning($"Sprite Tidy found nothing to keep in '{sourcePath}'.", textures[t]);
                        continue;
                    }

                    string folder = ResolveOutputFolder(sourcePath, outputFolder);
                    Directory.CreateDirectory(folder);
                    string baseName = Path.GetFileNameWithoutExtension(sourcePath);
                    for (int s = 0; s < result.Sprites.Count; s++)
                    {
                        var sprite = result.Sprites[s];
                        string name = result.Sprites.Count == 1 ? baseName : $"{baseName}_{s + 1:00}";
                        string path = $"{folder}/{name}.png";
                        SpriteTidyIO.SavePng(sprite.Image, path);

                        var pivot = preset.pivotFromAnchor ? new Vector2(sprite.PivotX, sprite.PivotY) : new Vector2(0.5f, 0.5f);
                        SpriteImportHook.Expect(path, preset.pixelsPerUnit, pivot);
                        written.Add(path);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // Refresh picks up new folders and changed files; files rewritten with identical bytes aren't reimported
            // by it, so force those to get this run's import settings.
            AssetDatabase.Refresh();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in written)
                    if (SpriteImportHook.IsPending(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return written;
        }

        public static string ResolveOutputFolder(string sourceAssetPath, string outputFolder)
        {
            if (!string.IsNullOrWhiteSpace(outputFolder)) return outputFolder.TrimEnd('/', '\\');

            // Package folders are read-only, so results from there go to a folder under Assets.
            if (!sourceAssetPath.StartsWith("Assets/", StringComparison.Ordinal)) return FallbackOutputFolder;
            return $"{Path.GetDirectoryName(sourceAssetPath).Replace('\\', '/')}/{DefaultSubfolder}";
        }
    }

    /// <summary>Applies sprite import settings to Sprite Tidy's output the first time each file is imported.</summary>
    sealed class SpriteImportHook : AssetPostprocessor
    {
        struct Request
        {
            public float PixelsPerUnit;
            public Vector2 Pivot;
        }

        static readonly Dictionary<string, Request> Pending = new Dictionary<string, Request>();

        public static void Expect(string assetPath, float pixelsPerUnit, Vector2 pivot)
        {
            Pending[assetPath] = new Request { PixelsPerUnit = pixelsPerUnit, Pivot = pivot };
        }

        public static bool IsPending(string assetPath) => Pending.ContainsKey(assetPath);

        void OnPreprocessTexture()
        {
            if (!Pending.TryGetValue(assetPath, out var request)) return;
            Pending.Remove(assetPath);

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = request.PixelsPerUnit;

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            textureSettings.spritePivot = request.Pivot;
            importer.SetTextureSettings(textureSettings);
        }
    }
}
