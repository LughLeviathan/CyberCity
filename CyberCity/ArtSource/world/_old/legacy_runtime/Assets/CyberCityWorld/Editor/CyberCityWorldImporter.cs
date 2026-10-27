#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CyberCityWorld.Editor
{
    // Requires the official 2D Sprite and 2D Tilemap Editor packages.
    // JSON coordinates start at the top left; Unity sprite rectangles start at the bottom left.
    public static class CyberCityWorldImporter
    {
        const string Root = "Assets/CyberCityWorld/Assets/CyberCityWorld";
        const string Generated = Root + "/Generated";
        const float PPU = 32f;

        [Serializable] public class Item
        {
            public int id, x, y, width, height;
            public string name, group;
            public bool solid;
            public int[] pivot, collision;
        }
        [Serializable] public class Sheet { public string name; public int width, height; public Item[] sprites; }
        [Serializable] public class Catalog { public int pixelsPerUnit; public Sheet[] sheets; }
        [Serializable] public class Placement { public string type, name; public int id, x, y; }
        [Serializable] public class Demo { public int width, height, tileSize; public int[] tileIds; public Placement[] objects; }

        static T Read<T>(string file)
        {
            if (!File.Exists(file)) throw new FileNotFoundException("CyberCity data was not copied to its expected path.", file);
            return JsonUtility.FromJson<T>(File.ReadAllText(file));
        }

        [MenuItem("Tools/CyberCity/1. Import and build assets")]
        public static void ImportAndBuild()
        {
            var catalog = Read<Catalog>(Root + "/Data/catalog.json");
            var demo = Read<Demo>(Root + "/Data/demo_map.json");
            foreach (var sheet in catalog.sheets) Slice(sheet);
            Directory.CreateDirectory(Generated + "/Tiles");
            AssetDatabase.Refresh();
            var terrain = catalog.sheets.First(s => s.name == "terrain");
            var sprites = SpriteLookup("terrain");
            var tiles = new Tile[terrain.sprites.Length];
            foreach (var item in terrain.sprites)
            {
                var path = Generated + "/Tiles/" + item.name + ".asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, path);
                }
                tile.name = item.name;
                tile.sprite = sprites[item.name];
                tile.color = Color.white;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
                tiles[item.id] = tile;
            }
            BuildPrefab(catalog, demo, tiles);
            AssetDatabase.SaveAssets();
            Debug.Log("CyberCity: 96 tiles, 24 props, 6 buildings and the lobby prefab are ready in " + Generated);
        }

        static void Slice(Sheet sheet)
        {
            var path = Root + "/Textures/" + sheet.name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Missing CyberCity texture", path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PPU;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("Enable the 2D Sprite package before importing CyberCity.");
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rects = sheet.sprites.Select(item => new SpriteRect
            {
                name = item.name,
                spriteID = existing.TryGetValue(item.name, out var guid) ? guid : GUID.Generate(),
                rect = new Rect(item.x, sheet.height - item.y - item.height, item.width, item.height),
                alignment = SpriteAlignment.Custom,
                pivot = new Vector2((float)item.pivot[0] / item.width, 1f - (float)item.pivot[1] / item.height)
            }).ToArray();
            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names != null) names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        static Dictionary<string, Sprite> SpriteLookup(string sheet)
        {
            return AssetDatabase.LoadAllAssetsAtPath(Root + "/Textures/" + sheet + ".png")
                .OfType<Sprite>().ToDictionary(s => s.name);
        }

        static Tilemap MakeTilemap(string name, Transform parent, bool visible)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = -10000;
            renderer.enabled = visible;
            return go.GetComponent<Tilemap>();
        }

        static void BuildPrefab(Catalog catalog, Demo demo, Tile[] tiles)
        {
            var root = new GameObject("CyberCity Lobby");
            try
            {
                var grid = new GameObject("Grid", typeof(Grid));
                grid.transform.SetParent(root.transform, false);
                var ground = MakeTilemap("Ground", grid.transform, true);
                var blocking = MakeTilemap("Water Collision", grid.transform, false);
                blocking.gameObject.AddComponent<TilemapCollider2D>();
                var blockerPath = Generated + "/WaterBlocker.asset";
                var blocker = AssetDatabase.LoadAssetAtPath<Tile>(blockerPath);
                if (blocker == null)
                {
                    blocker = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(blocker, blockerPath);
                }
                blocker.colliderType = Tile.ColliderType.Grid;
                EditorUtility.SetDirty(blocker);
                var terrain = catalog.sheets.First(s => s.name == "terrain");
                for (int y = 0; y < demo.height; y++)
                for (int x = 0; x < demo.width; x++)
                {
                    int id = demo.tileIds[y * demo.width + x];
                    var pos = new Vector3Int(x, -y - 1, 0);
                    ground.SetTile(pos, tiles[id]);
                    if (terrain.sprites[id].solid) blocking.SetTile(pos, blocker);
                }
                var propSprites = SpriteLookup("props");
                var buildingSprites = SpriteLookup("buildings");
                foreach (var p in demo.objects)
                {
                    string sheetName = p.type == "building" ? "buildings" : "props";
                    var item = catalog.sheets.First(s => s.name == sheetName).sprites[p.id];
                    var go = new GameObject(p.name, typeof(SpriteRenderer));
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = new Vector3(p.x / PPU, -p.y / PPU, 0);
                    var sr = go.GetComponent<SpriteRenderer>();
                    sr.sprite = (p.type == "building" ? buildingSprites : propSprites)[p.name];
                    sr.sortingOrder = p.y;
                    if (item.collision != null && item.collision.Length == 4)
                    {
                        var box = go.AddComponent<BoxCollider2D>();
                        var b = item.collision;
                        box.size = new Vector2(b[2] / PPU, b[3] / PPU);
                        box.offset = new Vector2((b[0] + b[2] * 0.5f - item.pivot[0]) / PPU,
                            -(b[1] + b[3] * 0.5f - item.pivot[1]) / PPU);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, Generated + "/CyberCityLobby.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem("Tools/CyberCity/2. Add demo to current scene")]
        public static void AddDemo()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/CyberCityLobby.prefab");
            if (prefab == null) { ImportAndBuild(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/CyberCityLobby.prefab"); }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Add CyberCity lobby");
            if (Camera.main == null)
            {
                var camGo = new GameObject("CyberCity Demo Camera", typeof(Camera));
                camGo.transform.SetParent(instance.transform, false);
                camGo.transform.localPosition = new Vector3(14, -11, -10);
                camGo.tag = "MainCamera";
                var camera = camGo.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 12;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(11, 16, 38, 255);
            }
            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.FrameSelected();
        }
    }
}
#endif
