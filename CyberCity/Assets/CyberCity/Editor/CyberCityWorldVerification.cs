#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.Rendering.Universal;

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
            Require(camera.assetsPPU == 32 && camera.refResolutionX == 1024 && camera.refResolutionY == 576, "Map camera uses selected canvas");
            for (int i = 0; i < 8; i++)
                Require(AssetDatabase.LoadAllAssetsAtPath("Assets/CyberCity/Generated/Citizen" + i + ".png").OfType<Sprite>().All(s => s.rect.width == 32 && s.rect.height == 32), "Character frames padded to 32x32");
            Debug.Log("CyberCity PASS: world scene contract");
        }
    }
}
#endif
