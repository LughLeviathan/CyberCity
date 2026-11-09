#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace CyberCity.Editor
{
    public static partial class CyberCityWorldBuilder
    {
        public static GameObject BuildWorld(Material material)
        {
            ImportArt(); var data = Read();
            var root = new GameObject("CyberCity / Reference world");
            var grid = new GameObject("32 pixel grid", typeof(Grid)); grid.transform.SetParent(root.transform);
            var ground = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer)); ground.transform.SetParent(grid.transform);
            var map = ground.GetComponent<Tilemap>(); var renderer = ground.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = material; renderer.sortingOrder = -1000;
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Textures + "terrain.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            Directory.CreateDirectory(Root + "/Tiles"); AssetDatabase.Refresh();
            var tiles = new Tile[sprites.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                string path = Root + "/Tiles/terrain_" + i.ToString("000") + ".asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile, path); }
                tile.sprite = sprites[i]; tile.colliderType = Tile.ColliderType.None; EditorUtility.SetDirty(tile); tiles[i] = tile;
            }
            for (int i = 0; i < data.tileIds.Length; i++) map.SetTile(new Vector3Int(i % 32, -1 - i / 32, 0), tiles[data.tileIds[i]]);
            var water = Image("Animated canal", AssetDatabase.LoadAllAssetsAtPath(Textures + "water.png").OfType<Sprite>().OrderBy(s => s.name).First(),
                WorldLayout.FromPixel(512, 576), root.transform, material, -990);
            water.gameObject.AddComponent<WorldWater>().frames = AssetDatabase.LoadAllAssetsAtPath(Textures + "water.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            Image("Skyline, gardens and seawall", Load("scenery_edge"), WorldLayout.FromPixel(512, 576), root.transform, material, -980);
            TMP_FontAsset font = LoadFont();
            var locks = AssetDatabase.LoadAllAssetsAtPath(Textures + "lock.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            foreach (var item in data.sprites)
            {
                var b = item.box;
                var position = WorldLayout.FromPixel((b[0] + b[2]) * .5f, b[3]);
                var go = new GameObject(item.area ?? item.id); go.transform.SetParent(root.transform); go.transform.position = position;
                var sort = go.AddComponent<SortingGroup>(); sort.sortingOrder = Mathf.RoundToInt(-position.y * 32) + 1;
                var image = Image(item.id, Load(item.id), position, go.transform, material, 0);
                if (item.kind != "building")
                {
                    // Narrow lamp/bench bases do not block the surrounding road.
                    if (b[2] - b[0] <= 60 && b[3] - b[1] < 80)
                    {
                        var col = go.AddComponent<BoxCollider2D>();
                        col.size = new Vector2(Mathf.Min((b[2] - b[0]) / 32f, .55f), .16f); col.offset = Vector2.up * .08f;
                    }
                    continue;
                }
                var glow = Image(item.id + " glow", Load(item.id + "_glow"), position, go.transform, material, 1);
                var label = Label(item.area, font, WorldLayout.FromPixel(item.label[0], item.label[1]), go.transform,
                    item.label[2] / 32f, item.label[3] / 32f, 2);
                label.gameObject.name = "Localized sign / " + item.area;
                if (item.area == "Player House" || item.area == "Cyber Corp") continue;
                var area = go.AddComponent<WorldArea>(); area.areaName = item.area; area.building = image; area.glow = glow;
                var door = new GameObject("Door / " + item.area); door.transform.SetParent(go.transform);
                door.transform.position = WorldLayout.FromPixel(item.door[0], item.door[1]); area.door = door.transform;
                area.lockIcon = Image("Area lock", locks[0], door.transform.position + Vector3.up * .65f, go.transform, material, 4);
                area.fragments = new[] {
                    Image("Lock fragment left", locks[1], area.lockIcon.transform.position, go.transform, material, 5),
                    Image("Lock fragment right", locks[2], area.lockIcon.transform.position, go.transform, material, 5) };
                foreach (var fragment in area.fragments) fragment.enabled = false;
                // Correct edit-mode preview; runtime initializes from the loaded save.
                bool initiallyOpen = CampaignLoader.UnlockForArea(CampaignLoader.Load(), item.area) == 0;
                image.color = initiallyOpen ? Color.white : new Color(.38f, .42f, .55f, 1);
                glow.enabled = initiallyOpen; area.lockIcon.enabled = !initiallyOpen;
            }
            Label("CYBERCITY", font, WorldLayout.FromPixel(493, 287), root.transform, 3.6f, .5f, 303);
            var collision = new GameObject("Static footprints / roads stay open"); collision.transform.SetParent(root.transform);
            foreach (var shape in data.colliders)
            {
                var go = new GameObject(shape.id); go.transform.SetParent(collision.transform);
                go.AddComponent<PolygonCollider2D>().points = shape.points.Select(p => WorldLayout.FromPixel(p.x, p.y)).ToArray();
            }
            return root;
        }

        static Sprite Load(string id) => AssetDatabase.LoadAssetAtPath<Sprite>(Textures + id + ".png");
        static SpriteRenderer Image(string name, Sprite sprite, Vector3 position, Transform parent, Material material, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer)); go.transform.SetParent(parent); go.transform.position = position;
            var sr = go.GetComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sharedMaterial = material; sr.sortingOrder = order; return sr;
        }
        static TMP_FontAsset LoadFont()
        {
            const string path = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            // Committed essential font/shader resources from the already installed UGUI package.
            if (font == null) throw new FileNotFoundException("Bundled TMP essential font is missing: " + path);
            return font;
        }
        static TextMeshPro Label(string text, TMP_FontAsset font, Vector3 position, Transform parent, float width, float height, int order)
        {
            var go = new GameObject(text, typeof(TextMeshPro)); go.transform.SetParent(parent); go.transform.position = position;
            var tmp = go.GetComponent<TextMeshPro>(); tmp.font = font; tmp.text = text;
            tmp.fontSize = 3; tmp.enableAutoSizing = true; tmp.fontSizeMin = 1.2f; tmp.fontSizeMax = 3;
            tmp.alignment = TextAlignmentOptions.Center; tmp.color = GameUI.White;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(width, height);
            tmp.GetComponent<MeshRenderer>().sortingOrder = order; return tmp;
        }
    }
}
#endif
