#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using System.Reflection;

namespace CyberCity.Editor
{
    public static class CyberCityWorldVerification
    {
        const string Root = "Assets/CyberCity/WorldArt";
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException("WORLD FAIL: " + message); }

        [MenuItem("Tools/CyberCity/Verify world art contract")]
        public static void VerifyArt()
        {
            Require(File.Exists(Root + "/world.json"), "New world data exists");
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Require(paths.Length >= 15, "Ground, buildings, glow, props and water exported");
            foreach (var path in paths)
            {
                var i = (TextureImporter)AssetImporter.GetAtPath(path);
                Require(i.spritePixelsPerUnit == 32 && i.filterMode == FilterMode.Point && !i.mipmapEnabled && i.textureCompression == TextureImporterCompression.Uncompressed, "Pixel import contract: " + path);
            }
            var water = AssetDatabase.LoadAllAssetsAtPath(Root + "/Textures/water.png").OfType<Sprite>().ToArray();
            Require(water.Length == 3, "Three water reflection frames");
            Debug.Log("CyberCity PASS: world art import contract");
        }

        [MenuItem("Tools/CyberCity/Verify world scene contract")]
        public static void VerifyScene()
        {
            Require(GameObject.Find("CyberCity / Reference world") != null, "Reference map replaces old prefab");
            var camera = Camera.main.GetComponent<PixelPerfectCamera>();
            Require(camera.assetsPPU == 32 && camera.refResolutionX == WorldLayout.CameraWidth && camera.refResolutionY == WorldLayout.CameraHeight, "Map camera includes HUD space when selecting integer zoom");
            for (int i = 0; i < 8; i++)
                Require(AssetDatabase.LoadAllAssetsAtPath("Assets/CyberCity/Generated/Citizen" + i + ".png").OfType<Sprite>().All(s => s.rect.width == 32 && s.rect.height == 32), "Character frames padded to 32x32");
            var world = GameObject.Find("CyberCity / Reference world");
            Require(GameObject.Find("CyberCity / Existing world art") == null, "Legacy prefab is absent");
            Require(world.GetComponentsInChildren<WorldArea>().Length == 4, "Only mission buildings have access visuals");
            foreach (var item in CyberCityWorldBuilder.Read().sprites.Where(s => s.kind == "building"))
            {
                var go = world.transform.Find(item.area);
                Require(go != null, "Building exists: " + item.area);
                var sort = go.GetComponent<UnityEngine.Rendering.SortingGroup>();
                Require(sort.sortingOrder == Mathf.RoundToInt(-go.position.y * 32) + 1, "Building Y sort: " + item.area);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CyberCityWorldBuilder.Textures + item.id + ".png");
                Require(sprite.pivot.y == 0 && Mathf.Abs(sprite.pivot.x - sprite.rect.width * .5f) < .01f, "Bottom center pivot: " + item.area);
                if (item.area == "Player House" || item.area == "Cyber Corp") Require(go.GetComponent<WorldArea>() == null, "Decoration has no lock");
            }
            Debug.Log("CyberCity PASS: world scene contract");
        }

        public static void VerifyProgression(CyberCityGame game)
        {
            string[] names = { "Cyber Academy", "Internet Cafe", "Linux Lab", "Web Security Lab" };
            int[] thresholds = { 0, 1, 4, 5 };
            int saved = game.Progress.completed;
            try
            {
                Require(game.areas.Length == 4, "Exactly four mission doors; decorations have no locks");
                for (int completed = 0; completed <= game.Campaign.missions.Length; completed++)
                {
                    game.Progress.completed = completed;
                    for (int i = 0; i < names.Length; i++)
                    {
                        var area = game.areas.Single(a => a.areaName == names[i]); area.Initialize(game);
                        bool expected = completed >= thresholds[i];
                        Require(area.UnlockAfter == thresholds[i] && area.IsOpen == expected, "Progression " + completed + ": " + names[i]);
                        Require(area.glow.enabled == expected && area.lockIcon.enabled != expected, "Lock/glow matches access");
                        Require(game.citizens.Where(n => n.area == names[i]).Any(n => completed >= n.unlockAfter) == expected, "Area opens with its first mentor; later mentor mission gates remain intact");
                        game.UI.CloseModal(); game.InteractArea(area);
                        bool locked = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude).Any(t => t.text.Contains("still locked"));
                        Require(locked != expected, "Door uses existing interaction gate at " + completed);
                        game.UI.CloseModal();
                    }
                }
                Require(game.areas.All(a => a.IsOpen), "No permanently locked area at campaign end");
            }
            finally
            {
                game.Progress.completed = saved;
                foreach (var area in game.areas) area.Initialize(game);
                game.UI.CloseModal(); game.UI.RefreshHud();
            }
            Debug.Log("CyberCity PASS: all 48 area/progression combinations, visual states and real door gates");
        }

        public static void VerifyFraming(CyberCityGame game, int width = 1920, int height = 1080)
        {
            Require(Screen.width == width && Screen.height == height, "Game View target is " + width + "x" + height);
            var topLeft = game.worldCamera.WorldToScreenPoint(Vector3.zero);
            var bottomRight = game.worldCamera.WorldToScreenPoint(new Vector3(WorldLayout.Width, -WorldLayout.Height, 0));
            Require(topLeft.x >= 0 && bottomRight.x <= Screen.width * (1248f / 1600), "Map fits left of mission console");
            Require(topLeft.y <= Screen.height * (814f / 900) && bottomRight.y >= Screen.height * (36f / 900), "Header/footer do not cover map");
            Require(WorldLayout.Clamp(new Vector2(-100, -100)) == new Vector2(.5f, -17.5f) && WorldLayout.Clamp(new Vector2(100, 100)) == new Vector2(31.5f, -.5f), "Map clamp matches new extents");
        }

        [MenuItem("Tools/CyberCity/Set world capture resolution")]
        public static void SetCaptureResolution() => SetGameViewResolution(1920, 1080);

        public static void SetGameViewResolution(int width, int height)
        {
            // Select a fixed Game View target; screenshots include the actual HUD at 1920x1080.
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { System.Enum.Parse(groupType, "Standalone") });
            var type = assembly.GetType("UnityEditor.GameViewSize");
            var sizeType = assembly.GetType("UnityEditor.GameViewSizeType");
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            int selected = -1;
            for (int i = 0; i < count; i++)
            {
                var candidate = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((int)type.GetProperty("width").GetValue(candidate) == width && (int)type.GetProperty("height").GetValue(candidate) == height)
                { selected = i; break; }
            }
            if (selected < 0)
            {
                var size = System.Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new object[] { System.Enum.Parse(sizeType, "FixedResolution"), width, height, "CyberCity verification " + width + "x" + height }, null);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size }); selected = count;
            }
            var gameViewType = assembly.GetType("UnityEditor.GameView");
            var window = EditorWindow.GetWindow(gameViewType);
            gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(window, selected);
            window.Repaint();
            Debug.Log("CyberCity Game View target: " + width + "x" + height);
        }
    }
}
#endif
