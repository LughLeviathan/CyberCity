#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace CyberCity.Editor
{
    public static class CyberCityMenuVerification
    {
        static CyberCityGame game;
        static Keyboard keyboard;
        static readonly List<Keyboard> paused = new List<Keyboard>();
        static readonly List<string> checks = new List<string>();
        static readonly Dictionary<string, byte[]> saves = new Dictionary<string, byte[]>();
        static string progress, language;
        static bool started, hadLanguage, hadSfx, hadVfx, running;
        static float sfx, vfx;
        static int width, height;
        public static string Status { get; private set; } = "Not run";
        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("MENU FAIL: " + message); checks.Add(message); }

        [MenuItem("Tools/CyberCity/Verify menu flow")]
        public static void Verify()
        {
            Require(EditorApplication.isPlaying, "Enter Play mode first");
            var game = UnityEngine.Object.FindAnyObjectByType<CyberCityGame>();
            game.UI.ShowStart();
            var buttons = game.UI.CanvasRoot.GetComponentsInChildren<Button>();
            Require(game.UI.CanvasRoot.GetComponentsInChildren<MenuSurface>().All(s => s.GetComponent<CanvasRenderer>() != null), "Rounded UI has renderers");
            Require(buttons.Any(b => b.GetComponentsInChildren<Text>().Any(t => t.text == "START" || t.text == "MULAI")), "Main menu offers Start before asking for identity");
            var start = buttons.First(b => b.GetComponentsInChildren<Text>().Any(t => t.text == "START" || t.text == "MULAI"));
            Require(game.UI.CanvasRoot.GetComponentsInChildren<InputField>().Length == 0, "Username is not embedded in main menu");
            start.onClick.Invoke();
            var input = game.UI.CanvasRoot.GetComponentInChildren<InputField>();
            Require(input != null && input.characterLimit == 16, "Start opens a 16-character identity field");
            input.text = "ab";
            var begin = game.UI.CanvasRoot.GetComponentsInChildren<Button>().First(b => b.GetComponentsInChildren<Text>().Any(t => t.text == "BEGIN PATROL" || t.text == "MULAI PATROLI"));
            begin.onClick.Invoke();
            Require(game.UI.CanvasRoot.GetComponentsInChildren<Text>().Any(t => t.color == GameUI.Gold && t.text.Length > 10), "Invalid identity gives an inline Gold error");
            game.UI.ShowStart();
            Debug.Log("CyberCity PASS: menu identity flow");
        }

        [MenuItem("Tools/CyberCity/Verify menu interactions and capture")]
        public static void StartInteractions()
        {
            if (!EditorApplication.isPlaying || running) throw new InvalidOperationException("Enter Play mode; only one menu verification can run.");
            game = UnityEngine.Object.FindAnyObjectByType<CyberCityGame>();
            progress = JsonUtility.ToJson(game.Progress); started = game.Started;
            language = PlayerPrefs.GetString(GameUI.LanguagePreference, "id"); hadLanguage = PlayerPrefs.HasKey(GameUI.LanguagePreference);
            sfx = PlayerPrefs.GetFloat(GameUI.SfxPreference, .7f); hadSfx = PlayerPrefs.HasKey(GameUI.SfxPreference);
            vfx = PlayerPrefs.GetFloat(GameUI.VfxPreference, 85); hadVfx = PlayerPrefs.HasKey(GameUI.VfxPreference);
            width = Screen.width; height = Screen.height;
            saves.Clear();
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            { string path = ProgressStore.SavePath + suffix; saves[path] = File.Exists(path) ? File.ReadAllBytes(path) : null; }
            paused.Clear();
            foreach (var device in InputSystem.devices.OfType<Keyboard>().Where(k => k.enabled).ToArray())
            { paused.Add(device); InputSystem.DisableDevice(device); }
            keyboard = InputSystem.AddDevice<Keyboard>("MenuVerificationKeyboard"); keyboard.MakeCurrent();
            PlayerPrefs.DeleteKey(GameUI.LanguagePreference); PlayerPrefs.DeleteKey(GameUI.SfxPreference); PlayerPrefs.DeleteKey(GameUI.VfxPreference);
            typeof(CyberCityGame).GetProperty("Started").GetSetMethod(true).Invoke(game, new object[] { false });
            game.Progress.started = false; game.Progress.playerName = "";
            game.UI.Initialize(game);
            checks.Clear(); running = true; Status = "Running";
            EditorApplication.playModeStateChanged += OnPlayMode;
            game.UI.StartCoroutine(Drive(Exercise()));
        }
        static IEnumerator Drive(IEnumerator routine)
        {
            while (true)
            {
                bool more;
                try { more = routine.MoveNext(); }
                catch (Exception e) { Status = e.Message; Debug.LogError(Status); break; }
                if (!more) { Status = "PASS: " + checks.Count + " menu checks"; break; }
                yield return routine.Current;
            }
            Directory.CreateDirectory("Documentation");
            File.WriteAllLines("Documentation/menu-verification.txt", new[] { Status }.Concat(checks.Select(c => "PASS " + c)));
            Restore(); Debug.Log("CyberCity menu verification: " + Status);
        }
        static Button Find(string name) => game.UI.CanvasRoot.GetComponentsInChildren<Button>().Single(b => b.name == name);
        static InputField Field => game.UI.CanvasRoot.GetComponentInChildren<InputField>();
        static void Press(Key key) { keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); }
        static void Release() => InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        static void Capture(string name)
        {
            Directory.CreateDirectory("Documentation/Screenshots/Menu");
            ScreenCapture.CaptureScreenshot("Documentation/Screenshots/Menu/" + name + ".png");
        }
        static void Click(string name)
        {
            Canvas.ForceUpdateCanvases(); var button = Find(name); var r = (RectTransform)button.transform;
            var eventData = new PointerEventData(EventSystem.current) { position = r.TransformPoint(r.rect.center) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(eventData, hits);
            Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "Pointer can reach " + name);
            ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
        }
        static IEnumerator Exercise()
        {
            CyberCityWorldVerification.SetGameViewResolution(1600, 900); yield return new WaitForSecondsRealtime(.2f);
            Require(game.UI.Language == "id", "Default language is Indonesian");
            Require(game.UI.CanvasRoot.GetComponentsInChildren<Text>().Where(t => t.fontSize == 112).All(t => t.cachedTextGenerator.vertexCount > 4), "Both title lines render with Silkscreen");
            Capture("01-main-menu"); yield return new WaitForSecondsRealtime(.08f);
            Press(Key.S); yield return new WaitForSecondsRealtime(.08f); Release();
            Require(EventSystem.current.currentSelectedGameObject.name == "Menu Settings", "S focuses Settings");
            yield return null; Press(Key.W); yield return new WaitForSecondsRealtime(.08f); Release();
            Require(EventSystem.current.currentSelectedGameObject.name == "Menu Start", "W focuses Start");
            yield return null; Press(Key.Enter); yield return new WaitForSecondsRealtime(.1f); Release();
            Require(Field != null && Field.isFocused && !game.Started, "Enter opens focused username without starting gameplay");
            Field.text = "ab"; Click("Username Begin");
            Require(!game.Started && game.UI.CanvasRoot.GetComponentsInChildren<Text>().Any(t => t.color == GameUI.Gold && t.text.Contains("3")), "Invalid short name reports localized Gold error");
            Field.text = "bad!"; Click("Username Begin"); Require(!game.Started, "Invalid character cannot begin");
            Field.text = "abcdefghijklmnopq"; Require(Field.text.Length == 16, "Input clamps pasted name to 16 characters");
            Field.text = "Byte_42"; yield return null; Canvas.ForceUpdateCanvases();
            Require(Field.textComponent.cachedTextGenerator.vertexCount > 4, "Input glyphs render within the field");
            Require(game.UI.CanvasRoot.GetComponentsInChildren<Text>().Any(t => t.text == "7/16"), "Counter follows input length");
            Capture("03-username"); yield return new WaitForSecondsRealtime(.08f);
            Press(Key.Escape); yield return new WaitForSecondsRealtime(.08f); Release();
            Require(Field == null && !game.Started, "ESC returns to menu without committing identity");
            Click("Menu Settings"); yield return null;
            var sliders = game.UI.CanvasRoot.GetComponentsInChildren<Slider>();
            Require(sliders[0].value == 70 && sliders[1].value == 85, "Default SFX/VFX values are 70/85");
            Canvas.ForceUpdateCanvases();
            Require(Mathf.Abs(sliders[0].fillRect.rect.width - 448) < .01f && Mathf.Abs(sliders[1].fillRect.rect.width - 544) < .01f, "Slider fill widths match percentages");
            Require(sliders.All(s => s.handleRect.rect.size == new Vector2(36,36)), "Thumb stays circular with 4px external stroke");
            Capture("02-settings"); yield return new WaitForSecondsRealtime(.08f);
            foreach (var slider in sliders)
                foreach (int v in new[] { 0, 100 })
                {
                    slider.value = v; Canvas.ForceUpdateCanvases();
                    Require(Mathf.Abs(slider.fillRect.rect.width - v * 6.4f) < .01f, slider.transform.parent.name + " endpoint " + v);
                    float center = slider.handleRect.parent.InverseTransformPoint(slider.handleRect.TransformPoint(slider.handleRect.rect.center)).x;
                    Require(Mathf.Abs(center - v * 6.4f) < .01f, "Thumb center follows endpoint " + v);
                }
            sliders[0].value = 0; sliders[1].value = 0;
            Require(game.effects.volume == 0 && PlayerPrefs.GetFloat(GameUI.SfxPreference) == 0, "Zero SFX mutes source and persists");
            Require(game.UI.VfxScale == 0 && game.areas.Where(a => a.IsOpen).All(a => a.glow.color.a == 0), "Zero VFX removes decorative glow");
            foreach (string code in MenuLocale.Codes)
            {
                Click("Language " + code); yield return null;
                Require(game.UI.Language == code && PlayerPrefs.GetString(GameUI.LanguagePreference) == code, "Language applies and persists: " + code);
                Require(game.UI.CanvasRoot.GetComponentsInChildren<Slider>().All(s => s.value == 0), "Language refresh preserves both zero settings");
                Require(game.UI.CanvasRoot.GetComponentsInChildren<Text>().Any(t => t.text == MenuLocale.Get("settings.title", code)), "Localized settings title: " + code);
                Capture("language-" + code); yield return new WaitForSecondsRealtime(.08f);
            }
            Click("Language id"); yield return new WaitForSecondsRealtime(.08f); Click("Settings Close"); yield return null;
            Require(Field == null && Find("Menu Start") != null, "Close button returns to menu");
            game.UI.Initialize(game); yield return null; Click("Menu Settings"); yield return null;
            Require(game.UI.CanvasRoot.GetComponentsInChildren<Slider>().All(s => s.value == 0), "Settings reload from preferences, including zero");
            var restoredSliders = game.UI.CanvasRoot.GetComponentsInChildren<Slider>(); restoredSliders[0].value = 70; restoredSliders[1].value = 85;
            Press(Key.Escape); yield return new WaitForSecondsRealtime(.08f); Release();
            foreach (var resolution in new[] { new Vector2Int(2560,1080), new Vector2Int(1440,1080) })
            {
                CyberCityWorldVerification.SetGameViewResolution(resolution.x,resolution.y); yield return new WaitForSecondsRealtime(.2f);
                CyberCityFullscreenVerification.VerifyWelcome(game); Capture("menu-" + resolution.x + "x" + resolution.y); yield return new WaitForSecondsRealtime(.08f);
                Click("Menu Settings"); yield return null;
                var panel = game.UI.CanvasRoot.GetComponentsInChildren<MenuSurface>().Single(s => s.name == "Settings").rectTransform;
                var corners = new Vector3[4]; panel.GetWorldCorners(corners);
                Require(corners[0].x >= 0 && corners[0].y >= 0 && corners[2].x <= Screen.width && corners[2].y <= Screen.height, "Settings remains on screen at " + resolution);
                Capture("settings-" + resolution.x + "x" + resolution.y); yield return new WaitForSecondsRealtime(.08f); Click("Settings Back"); yield return null;
            }
            CyberCityWorldVerification.SetGameViewResolution(1600,900); yield return new WaitForSecondsRealtime(.2f);
            game.Progress.started = true; game.Progress.playerName = "Byte_42";
            Click("Menu Start"); yield return null; Require(Field.text == "Byte_42", "Returning player is prefilled via the same Start flow");
            Field.text = "  Byte_42  "; Press(Key.Enter); yield return new WaitForSecondsRealtime(.08f); Release();
            Require(game.UI.FrontEndActive && !game.Started && game.Progress.playerName == "Byte_42", "Valid Enter trims name and starts transition only");
            yield return new WaitForSecondsRealtime(.5f); Capture("04-gameplay-transition");
            Press(Key.Escape); yield return new WaitForSecondsRealtime(.08f); Release();
            Require(!game.Started && game.UI.HasModal, "ESC cannot skip welcome transition");
            yield return new WaitForSecondsRealtime(1f);
            Require(game.Started && !game.UI.FrontEndActive && !game.UI.HasModal, "Transition finishes once and releases gameplay");
            Require(game.player.CanMove && EventSystem.current.sendNavigationEvents, "Gameplay movement and navigation restored");
            Require(ProgressStore.Load(game.Campaign).playerName == "Byte_42", "Begin persists trimmed identity");
            Capture("05-gameplay"); yield return new WaitForSecondsRealtime(.08f);
        }
        static void OnPlayMode(PlayModeStateChange change)
        { if (change == PlayModeStateChange.ExitingPlayMode && running) { Status = "Interrupted"; Restore(); } }
        static void Restore()
        {
            if (!running) return; running = false;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            foreach (var k in paused) if (k.added) InputSystem.EnableDevice(k);
            paused.Clear();
            if (hadLanguage) PlayerPrefs.SetString(GameUI.LanguagePreference, language); else PlayerPrefs.DeleteKey(GameUI.LanguagePreference);
            if (hadSfx) PlayerPrefs.SetFloat(GameUI.SfxPreference, sfx); else PlayerPrefs.DeleteKey(GameUI.SfxPreference);
            if (hadVfx) PlayerPrefs.SetFloat(GameUI.VfxPreference, vfx); else PlayerPrefs.DeleteKey(GameUI.VfxPreference);
            PlayerPrefs.Save();
            if (game != null)
            {
                JsonUtility.FromJsonOverwrite(progress, game.Progress);
                typeof(CyberCityGame).GetProperty("Started").GetSetMethod(true).Invoke(game, new object[] { started });
                game.UI.Initialize(game); foreach (var area in game.areas) area.Initialize(game);
            }
            foreach (var save in saves)
                if (save.Value != null) File.WriteAllBytes(save.Key, save.Value); else if (File.Exists(save.Key)) File.Delete(save.Key);
            CyberCityWorldVerification.SetGameViewResolution(width,height);
        }
    }
}
#endif
