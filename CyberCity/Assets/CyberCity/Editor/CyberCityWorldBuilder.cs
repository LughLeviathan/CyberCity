#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace CyberCity.Editor
{
    public static partial class CyberCityWorldBuilder
    {
        public const string Root = "Assets/CyberCity/WorldArt";
        public const string Textures = Root + "/Textures/";
        [Serializable] public class WorldData
        {
            public int width, height, tileSize, columns, rows, uniqueTileCount;
            public int[] tileIds;
            public WorldSprite[] sprites;
            public WorldShape[] colliders;
        }
        [Serializable] public class WorldSprite
        {
            public string id, area, kind;
            public int[] box, door, label;
        }
        [Serializable] public class WorldShape
        {
            public string id;
            public Vector2[] points;
        }
        public static WorldData Read() => JsonUtility.FromJson<WorldData>(File.ReadAllText(Root + "/world.json"));

        [MenuItem("Tools/CyberCity/Import reference world art")]
        public static void ImportArt()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var data = Read();
            foreach (string path in Directory.GetFiles(Textures, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                string id = Path.GetFileNameWithoutExtension(path);
                bool multiple = id == "terrain" || id == "water" || id == "lock";
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = multiple ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 4096;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePivot = new Vector2(.5f, 0);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
                if (!multiple) continue;
                var factory = new SpriteDataProviderFactories(); factory.Init();
                var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                var old = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
                int count = id == "terrain" ? data.uniqueTileCount : 3;
                var rects = new SpriteRect[count];
                for (int i = 0; i < count; i++)
                {
                    string name = id + "_" + i.ToString("000");
                    Rect rect = id == "terrain" ? new Rect(i % data.columns * 32, (data.rows - 1 - i / data.columns) * 32, 32, 32) :
                        id == "lock" ? (i == 0 ? new Rect(0, 0, 16, 20) : new Rect((i - 1) * 8, 0, 8, 20)) : new Rect(i * data.width, 0, data.width, data.height);
                    rects[i] = new SpriteRect { name = name, rect = rect, alignment = SpriteAlignment.Custom,
                        pivot = id == "terrain" ? new Vector2(.5f, .5f) : new Vector2(.5f, 0),
                        spriteID = old.TryGetValue(name, out var guid) ? guid : GUID.Generate() };
                }
                provider.SetSpriteRects(rects);
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
                provider.Apply(); importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("CyberCity: reference art imported with PPU32 / Point / lossless / no mipmaps.");
        }
    }
}
#endif
