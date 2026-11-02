#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CyberCity.Editor
{
    public static class CyberCityFullscreenVerification
    {
        static void Require(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("FULLSCREEN FAIL: " + message); }

        [MenuItem("Tools/CyberCity/Verify fullscreen scene")]
        public static void VerifyScene()
        {
            var camera = Camera.main.GetComponent<PixelPerfectCamera>();
            Require(camera.refResolutionX == 1024 && camera.refResolutionY == 576,
                "Camera reference matches the complete map without console padding");
            Require(camera.cropFrame == PixelPerfectCamera.CropFrame.StretchFill &&
                camera.gridSnapping == PixelPerfectCamera.GridSnapping.UpscaleRenderTexture,
                "Fit the complete map at fractional screen scales without distortion");
            Require(new SerializedObject(camera).FindProperty("m_FilterMode").enumValueIndex == (int)PixelPerfectCamera.PixelPerfectFilterMode.Point,
                "Final upscale uses Point, not RetroAA bilinear filtering");
            Debug.Log("CyberCity PASS: fullscreen scene contract");
        }

        public static void VerifyHud(CyberCityGame game)
        {
            var hud = game.UI.CanvasRoot.Find("Patrol HUD");
            Require(hud != null && hud.Find("Mission console") == null, "Main HUD has no mission console");
            var texts = hud.GetComponentsInChildren<Text>(true);
            Require(!texts.Any(t => t.text.Contains("CURRENT OBJECTIVE") || t.text.Contains("CITY DISTRICTS") ||
                t.text.Contains("YOUR PATROL") || t.text.Contains("LOCAL ONLY") || t.text.Contains("OFFLINE PACK")), "Objective and program metadata are absent from main HUD");
            var slot = hud.GetComponentsInChildren<RectTransform>(true).SingleOrDefault(t => t.name == "QuestButtonSlot");
            Require(slot != null && slot.GetComponentInChildren<Button>() != null, "Quest slot contains a usable button");
            Require(slot.GetComponentInChildren<Text>().text == "J  Journal", "Journal remains available in quest slot");
            int savedScore = game.Progress.score; string savedName = game.Progress.playerName;
            try
            {
                game.Progress.score = 321; game.Progress.playerName = "Hud_Probe"; game.UI.RefreshHud();
                Require(texts.Any(t => t.text == "0321  XP"), "XP refreshes without an objective count field");
                Require(game.UI.CanvasRoot.Find("World labels").GetComponentsInChildren<Text>(true).Any(t => t.text == "Hud_Probe"), "Player label refreshes without an objective count field");
            }
            finally { game.Progress.score = savedScore; game.Progress.playerName = savedName; game.UI.RefreshHud(); }
            Require(!hud.GetComponentsInChildren<Image>(true).Any(i => i.color.a > .7f && i.rectTransform.rect.width > 500), "No solid full width header or footer");
        }

        public static void VerifyModal(CyberCityGame game)
        {
            Canvas.ForceUpdateCanvases();
            var panel = game.UI.Content.parent as RectTransform;
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            Require(corners.All(p => p.x >= -1 && p.x <= Screen.width + 1 && p.y >= -1 && p.y <= Screen.height + 1), "Modal panel stays inside display");
            Require(Vector2.Distance((corners[0] + corners[2]) / 2, new Vector2(Screen.width / 2f, Screen.height / 2f)) < 2, "Modal panel is centered independently of the HUD");
        }

        public static void VerifyWelcome(CyberCityGame game)
        {
            Canvas.ForceUpdateCanvases();
            var root = (RectTransform)game.UI.CanvasRoot.Find("Welcome");
            var corners = new Vector3[4]; root.GetWorldCorners(corners);
            Require(corners[0].x <= 1 && corners[0].y <= 1 && corners[2].x >= Screen.width - 1 && corners[2].y >= Screen.height - 1, "Welcome background covers whole display");
            var content = (RectTransform)root.Find("Welcome content"); content.GetWorldCorners(corners);
            Require(Vector2.Distance((corners[0] + corners[2]) / 2, new Vector2(Screen.width / 2f, Screen.height / 2f)) < 2, "Welcome content stays centered on wide and tall displays");
        }

        public static void VerifyLabels(CyberCityGame game)
        {
            Canvas.ForceUpdateCanvases(); game.UI.UpdateWorldLabels();
            var labels = game.UI.CanvasRoot.Find("World labels").GetComponentsInChildren<Text>();
            var targetName = labels.Single(t => t.text == game.Target.citizenName);
            var playerName = labels.Single(t => t.text == game.Progress.playerName);
            var marker = labels.Single(t => t.text == "!");
            float scale = Mathf.Min(Screen.width / 1024f, Screen.height / 576f);
            var origin = new Vector2((Screen.width - 1024 * scale) / 2, (Screen.height - 576 * scale) / 2);
            Action<Text, Vector3> registered = (text, world) => {
                Vector2 expected = origin + new Vector2(world.x * 32, (world.y + 18) * 32) * scale;
                var r = text.rectTransform;
                Vector2 actual = r.TransformPoint(new Vector3(r.rect.center.x, r.rect.yMax));
                Require(Vector2.Distance(actual, expected) < 2, "World label registration: " + text.text);
            };
            registered(targetName, game.Target.transform.position + Vector3.down * .12f);
            registered(playerName, game.player.transform.position + (game.Nearest != null ? Vector3.down * .3f : Vector3.up * 1.05f));
            registered(marker, game.Target.transform.position + Vector3.up * (1.25f + Mathf.Sin(Time.time * 3) * .08f));
            var prompt = game.UI.CanvasRoot.Find("Patrol HUD/Interaction prompt").GetComponent<Text>();
            Require(prompt.text.StartsWith("[ E ]"), "Nearby mentor or door exposes E prompt");
            var corners = new Vector3[4]; prompt.rectTransform.GetWorldCorners(corners);
            var playerPoint = game.worldCamera.WorldToScreenPoint(game.player.transform.position);
            Require(corners[0].y > 40 * game.UI.CanvasRoot.lossyScale.x, "Prompt stays clear of footer");
            Require(playerPoint.y > corners[2].y + 10 || playerPoint.y < corners[0].y - 10, "Prompt does not cover player");
        }
    }
}
#endif
