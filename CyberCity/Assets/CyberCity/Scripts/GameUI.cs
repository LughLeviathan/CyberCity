using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CyberCity
{
    [DefaultExecutionOrder(20)]
    public sealed class GameUI : MonoBehaviour
    {
        public Sprite cityPreview;
        public static readonly Color Ink = Hex("090F22"), Panel = Hex("101A33"), Raised = Hex("1B2947"),
            Cyan = Hex("00E5FF"), Gold = Hex("FFB347"), Teal = Hex("00D4B7"), Violet = Hex("8B5CF6"),
            White = Hex("FFF4D6"), Muted = Hex("8EA5C5"), Pink = Hex("FF2D9B");
        public CyberCityGame Game { get; private set; }
        public RectTransform CanvasRoot { get; private set; }
        public RectTransform Content { get; private set; }
        // After Initialize, replace the Journal child with your own button under this slot.
        public RectTransform QuestButtonSlot { get; private set; }
        public bool HasModal => modal != null || transitionOverlay != null;
        public bool FrontEndActive => frontScreen != FrontScreen.None;
        public const string LanguagePreference = "CyberCityLanguage", SfxPreference = "CyberCityEffects", VfxPreference = "CyberCityVfx";
        public string Language { get; private set; } = "id";
        public float VfxIntensity { get; private set; } = 85;
        public float VfxScale => VfxIntensity / 100f;
        enum FrontScreen { None, Main, Settings, Username, Transition }
        enum MenuVariant { Primary, Secondary, Quiet }
        FrontScreen frontScreen;
        Font menuTitleFont, pixelFont, bodyFont, mediumFont, semiboldFont;
        RectTransform welcomeContent;
        GameObject frontLayer, transitionOverlay;
        CanvasGroup menuGroup;
        InputField username;
        Text usernameHelp;
        MenuSurface usernameSurface;
        readonly List<Selectable> frontControls = new List<Selectable>();
        readonly List<Graphic> menuLights = new List<Graphic>();
        Coroutine transitionRoutine;
        bool previousNavigation;
        int frontOpenedFrame;
        Font font;
        GameObject modal;
        RectTransform hud, labels;
        Text score, prompt, toast;
        RectTransform[] cropBars;
        readonly Dictionary<Citizen, Text> nameLabels = new Dictionary<Citizen, Text>();
        readonly Dictionary<Citizen, Text> markers = new Dictionary<Citizen, Text>();
        Text playerLabel;
        float toastUntil;
        ModuleUI modules;

        string L(string key) => MenuLocale.Get(key, Language);
        static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }
        void LoadMenuSettings()
        {
            string savedLanguage = PlayerPrefs.GetString(LanguagePreference, "id");
            Language = Array.IndexOf(MenuLocale.Codes, savedLanguage) >= 0 ? savedLanguage : "id";
            VfxIntensity = Mathf.Clamp(PlayerPrefs.GetFloat(VfxPreference, 85), 0, 100);
            if (Game?.effects != null) Game.effects.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxPreference, .7f));
            menuTitleFont = Resources.Load<Font>("CyberCityFonts/Silkscreen-Bold");
            pixelFont = Resources.Load<Font>("CyberCityFonts/PixelifySans-Bold");
            bodyFont = Resources.Load<Font>("CyberCityFonts/ChakraPetch-Regular");
            mediumFont = Resources.Load<Font>("CyberCityFonts/ChakraPetch-Medium");
            semiboldFont = Resources.Load<Font>("CyberCityFonts/ChakraPetch-SemiBold");
        }

        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color c); return c; }
        void EnsureCanvas()
        {
            if (CanvasRoot != null) return;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("CyberCity Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            CanvasRoot = (RectTransform)go.transform;
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            r.SetParent(parent, false); r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        public RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var r = Rect(parent, name, x, y, w, h); r.gameObject.AddComponent<Image>().color = color; return r;
        }
        public Text Text(Transform parent, string value, float x, float y, float w, float h, int size = 20, Color? color = null, bool bold = false)
        {
            var r = Rect(parent, "Text", x, y, w, h); var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = color ?? White; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.supportRichText = false; t.text = Game == null ? value : Game.Personalize(value); t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }
        public Button Button(Transform parent, string label, float x, float y, float w, float h, Action action, Color? accent = null)
        {
            var r = Box(parent, label, x, y, w, h, accent ?? Raised);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = r.GetComponent<Image>();
            var colors = b.colors; colors.highlightedColor = new Color(.75f,.9f,1); colors.pressedColor = new Color(.55f,.7f,.85f); b.colors = colors;
            var t = Text(r, label, 16, 0, w - 32, h, 18, accent.HasValue ? Ink : White, true); t.alignment = TextAnchor.MiddleLeft;
            b.onClick.AddListener(() => { if (Game != null) Game.Play(Game.clickSound); action(); }); return b;
        }
        public InputField Input(Transform parent, float x, float y, float w, float h, string placeholder)
        {
            var r = Box(parent, "Input", x, y, w, h, Ink); var input = r.gameObject.AddComponent<InputField>();
            var value = Text(r, "", 14, 8, w - 28, h - 16, 20, Cyan); value.alignment = TextAnchor.MiddleLeft;
            var hint = Text(r, placeholder, 14, 8, w - 28, h - 16, 18, Muted); hint.alignment = TextAnchor.MiddleLeft;
            input.textComponent = value; input.placeholder = hint; input.characterLimit = 200; return input;
        }

        public RectTransform Scroll(Transform parent, string name, float x, float y, float w, float h, float contentHeight)
        {
            var viewport = Box(parent, name, x, y, w, h, Panel);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Scrollable content", 0, 0, w, Mathf.Max(h, contentHeight));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
            return content;
        }

        public void Initialize(CyberCityGame game)
        {
            CancelTransition(); RestoreNavigation();
            if (CanvasRoot != null) Destroy(CanvasRoot.gameObject);
            CanvasRoot = null; modal = null; Content = null;
            nameLabels.Clear(); markers.Clear();
            Game = game; EnsureCanvas(); LoadMenuSettings(); modules = new ModuleUI(this);
            cropBars = new RectTransform[4];
            for (int i = 0; i < cropBars.Length; i++)
            {
                cropBars[i] = Box(CanvasRoot, "Map crop bar " + i, 0, 0, 0, 0, Ink);
                cropBars[i].GetComponent<Image>().raycastTarget = false;
            }
            labels = Rect(CanvasRoot, "World labels", 0, 0, 1600, 900);
            foreach (var citizen in game.citizens)
            {
                var t = Text(labels, citizen.citizenName, 0, 0, 190, 24, 15, citizen.accent, true); t.alignment = TextAnchor.MiddleCenter;
                var outline = t.gameObject.AddComponent<Outline>(); outline.effectColor = Ink; outline.effectDistance = new Vector2(1, -1);
                nameLabels[citizen] = t;
                var marker = Text(labels, "!", 0, 0, 36, 34, 28, Gold, true); marker.alignment = TextAnchor.MiddleCenter;
                marker.gameObject.AddComponent<Outline>().effectColor = Ink; markers[citizen] = marker;
            }
            playerLabel = Text(labels, "{player}", 0, 0, 220, 24, 14, Cyan, true); playerLabel.alignment = TextAnchor.MiddleCenter;
            OutlineLabel(playerLabel);
            BuildHud(); ShowStart();
        }

        void BuildHud()
        {
            hud = Rect(CanvasRoot, "Patrol HUD", 0, 0, 1600, 900); Stretch(hud);
            var title = Text(hud, "CYBERCITY", 24, 20, 176, 32, 22, White, true); OutlineLabel(title);
            score = Text(hud, "", 218, 21, 190, 32, 20, Gold, true); OutlineLabel(score);
            QuestButtonSlot = Rect(hud, "QuestButtonSlot", 0, 0, 146, 40);
            Anchor(QuestButtonSlot, Vector2.one, Vector2.one, new Vector2(-180, -16));
            var journal = Button(QuestButtonSlot, "J  Journal", 0, 0, 146, 40, ShowJournal);
            journal.GetComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, .55f);
            var pause = Button(hud, "ESC  Menu", 0, 0, 144, 40, ShowPause);
            Anchor(pause.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-24, -16));
            pause.GetComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, .55f);
            var controls = Text(hud, "WASD  Move     E  Interact     J  Journal     M  Map     H  Field guide", 0, 0, 850, 26, 15, White);
            controls.gameObject.name = "Controls"; controls.alignment = TextAnchor.MiddleCenter; OutlineLabel(controls);
            Anchor(controls.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 12));
            prompt = Text(hud, "", 0, 0, 440, 34, 18, Gold, true);
            prompt.gameObject.name = "Interaction prompt"; prompt.alignment = TextAnchor.MiddleCenter; OutlineLabel(prompt);
            Anchor(prompt.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 48));
            toast = Text(hud, "", 0, 0, 1100, 30, 17, Teal, true);
            toast.gameObject.name = "Patrol toast"; toast.alignment = TextAnchor.MiddleCenter; OutlineLabel(toast);
            Anchor(toast.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -64));
            RefreshHud();
        }

        public void RefreshHud()
        {
            if (Game?.Progress == null) return;
            if (playerLabel != null) playerLabel.text = Game.Personalize("{player}");
            if (score != null) score.text = Game.Progress.score.ToString("0000") + "  XP";
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; }
        static void OutlineLabel(Text text)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = Ink; outline.effectDistance = new Vector2(1, -1);
        }
        void LateUpdate()
        {
            if (Game == null || CanvasRoot == null) return;
            UpdateCropBars(); UpdateWorldLabels();
        }
        void UpdateCropBars()
        {
            // URP clears cropped borders black; tint only the unused screen space.
            if (cropBars == null || Game.worldCamera == null) return;
            var viewport = Game.worldCamera.rect;
            Vector2[] mins = { Vector2.zero, new Vector2(viewport.xMax, 0), new Vector2(viewport.xMin, 0), new Vector2(viewport.xMin, viewport.yMax) };
            Vector2[] maxs = { new Vector2(viewport.xMin, 1), Vector2.one, new Vector2(viewport.xMax, viewport.yMin), new Vector2(viewport.xMax, 1) };
            for (int i = 0; i < cropBars.Length; i++)
            {
                cropBars[i].anchorMin = mins[i]; cropBars[i].anchorMax = maxs[i];
                cropBars[i].offsetMin = cropBars[i].offsetMax = Vector2.zero;
            }
        }

        void PositionLabel(Text label, Vector3 world, float dx = 0)
        {
            Vector3 p = Game.worldCamera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRoot, p, null, out Vector2 point);
            var rect = label.rectTransform;
            rect.position = CanvasRoot.TransformPoint(point);
            rect.position += new Vector3((-rect.rect.width * .5f + dx) * CanvasRoot.lossyScale.x, 0, 0);
        }
        public void UpdateWorldLabels()
        {
            if (Game == null || labels == null || modules == null) return;
            labels.gameObject.SetActive(Game.Started && !HasModal);
            foreach (var n in Game.citizens)
            {
                nameLabels[n].gameObject.SetActive(n == Game.Target || n == Game.Nearest);
                PositionLabel(nameLabels[n], n.transform.position + Vector3.down * .12f);
                markers[n].gameObject.SetActive(n == Game.Target);
                PositionLabel(markers[n], n.transform.position + Vector3.up * (1.25f + Mathf.Sin(Time.time * 3) * .08f));
            }
            PositionLabel(playerLabel, Game.player.transform.position + (Game.Nearest != null ? Vector3.down * .3f : Vector3.up * 1.05f));
            prompt.text = HasModal ? "" : Game.DoorIsNearest ? "[ E ]  " + Game.NearestDoor.areaName :
                Game.Nearest != null ? "[ E ]  Talk to " + Game.Nearest.citizenName : "";
            // All interactable doors/NPCs are above the southern waterfront. Keep this hint
            // in that clear lower strip so it cannot cover their sprites or the plaza globe.
            hud.gameObject.SetActive(Game.Started);
            if (Time.unscaledTime > toastUntil) toast.text = "";
        }

        public void Toast(string message) { if (toast != null) { toast.text = Game.Personalize(message); toastUntil = Time.unscaledTime + 6; } }

        public void CloseModal()
        {
            if (frontScreen == FrontScreen.Transition) return;
            if (frontScreen == FrontScreen.Settings || frontScreen == FrontScreen.Username) { BackToMenu(); return; }
            if (Game != null && !Game.Started) return;
            RemoveModal();
            EventSystem.current?.SetSelectedGameObject(null);
        }
        void RemoveModal()
        {
            if (modal != null) { modal.SetActive(false); Destroy(modal); }
            modal = null; Content = null; frontLayer = null; username = null;
            frontControls.Clear(); menuLights.Clear(); RestoreNavigation(); frontScreen = FrontScreen.None;
        }
        public RectTransform OpenModal(string kicker, string title, string subtitle = "")
        {
            EnsureCanvas(); RemoveModal();
            modal = Box(CanvasRoot, "Modal scrim", 0, 0, 1600, 900, new Color(.015f,.025f,.065f,.94f)).gameObject;
            Stretch((RectTransform)modal.transform);
            var panel = Box(modal.transform, "Module panel", 0, 0, 1120, 696, Panel);
            Anchor(panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero);
            Box(panel, "Top accent", 0, 0, 1120, 3, Cyan);
            Text(panel, kicker.ToUpperInvariant(), 32, 25, 1010, 23, 12, Cyan, true);
            Text(panel, title, 32, 58, 990, 49, 34, White, true);
            Text(panel, subtitle, 32, 113, 1020, 48, 17, Muted);
            Button(panel, "X", 1040, 22, 48, 40, CloseModal);
            Content = Rect(panel, "Module content", 32, 170, 1056, 492);
            return Content;
        }

        // Front-end screens keep the existing top-left 1600x900 coordinate system.
        MenuSurface Rounded(Transform parent, string name, float x, float y, float w, float h,
            Color fill, float radius = 16, Color? stroke = null, float strokeWidth = 2)
        {
            var surface = Rect(parent, name, x, y, w, h).gameObject.AddComponent<MenuSurface>();
            surface.color = fill; surface.radius = radius; surface.stroke = stroke ?? fill;
            surface.strokeWidth = stroke.HasValue ? strokeWidth : 0;
            return surface;
        }
        Text MenuText(Transform parent, string value, float x, float y, float w, float h,
            int size, Color color, Font face = null, float tracking = 0, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var t = Text(parent, value, x, y, w, h, size, color);
            t.font = face != null ? face : bodyFont != null ? bodyFont : font;
            t.alignment = align;
            if (tracking != 0) t.gameObject.AddComponent<MenuTracking>().spacing = size * tracking;
            return t;
        }
        Button MenuAction(Transform parent, string name, string index, string key, float x, float y,
            float width, Action action, MenuVariant variant)
        {
            Color fill = variant == MenuVariant.Primary ? Cyan : variant == MenuVariant.Secondary ? Raised : Ink;
            Color text = variant == MenuVariant.Primary ? Ink : variant == MenuVariant.Secondary ? White : Muted;
            Color? stroke = variant == MenuVariant.Primary ? (Color?)null : Alpha(variant == MenuVariant.Secondary ? Cyan : Muted, .35f);
            var surface = Rounded(parent, name, x, y, width, 72, fill, 16, stroke);
            var button = surface.gameObject.AddComponent<MenuButton>();
            button.surface = surface; button.restingStroke = surface.stroke; button.targetGraphic = surface;
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = colors.selectedColor = new Color(1.12f, 1.12f, 1.12f, 1);
            colors.pressedColor = new Color(.85f, .85f, .85f, 1); colors.fadeDuration = .08f; button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            // Put the label first in the hierarchy for existing automation/accessibility consumers.
            float indexWidth = index.Length > 3 ? 56 : index.Length > 2 ? 38 : 20;
            float labelX = 24 + indexWidth + 16;
            string label = L(key);
            int labelSize = 26;
            if (label.Length > 14) labelSize = 23;
            MenuText(surface.transform, label, labelX, 0, width - labelX - 64, 72, labelSize, text, menuTitleFont, .06f);
            MenuText(surface.transform, index, 24, 0, indexWidth + 4, 72, 14, Alpha(text, .65f), semiboldFont, .12f);
            MenuText(surface.transform, L("icon.arrow"), width - 46, 0, 26, 72, 26, text, pixelFont, 0, TextAnchor.MiddleCenter);
            button.onClick.AddListener(() => { if (Game != null) Game.Play(Game.clickSound); action(); });
            frontControls.Add(button); return button;
        }
        void TakeNavigation()
        {
            if (frontScreen == FrontScreen.None && EventSystem.current != null)
            {
                previousNavigation = EventSystem.current.sendNavigationEvents;
                EventSystem.current.sendNavigationEvents = false;
            }
        }
        void RestoreNavigation()
        {
            if (frontScreen != FrontScreen.None && EventSystem.current != null)
                EventSystem.current.sendNavigationEvents = previousNavigation;
        }
        public void ShowStart()
        {
            CancelTransition(); RemoveModal(); EnsureCanvas();
            if (bodyFont == null) LoadMenuSettings();
            TakeNavigation(); frontScreen = FrontScreen.Main; frontOpenedFrame = Time.frameCount;
            modal = Box(CanvasRoot, "Welcome", 0, 0, 1600, 900, Ink).gameObject;
            Stretch((RectTransform)modal.transform);
            welcomeContent = Rect(modal.transform, "Welcome content", 0, 0, 1600, 900);
            Anchor(welcomeContent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero);
            menuGroup = welcomeContent.gameObject.AddComponent<CanvasGroup>();
            BuildMenuContents();
            SelectControl(0);
        }
        void BuildMenuContents()
        {
            frontControls.Clear(); menuLights.Clear();
            BuildSkyline(welcomeContent);
            var fade = Rect(welcomeContent, "Left fade", 0, 0, 980, 900).gameObject.AddComponent<MenuGradient>();
            fade.color = Ink; fade.raycastTarget = false;
            MenuText(welcomeContent, L("menu.eyebrow"), 80, 92, 600, 24, 14, Cyan, semiboldFont);
            // Unity's font line metric includes extra leading; use explicit baseline separation.
            string[] titleLines = L("menu.title").Split('\n');
            for (int i = 0; i < titleLines.Length; i++)
                MenuText(welcomeContent, titleLines[i], 76, 128 + i * 112 * .88f, 650, 112, 112, White, menuTitleFont, .02f).verticalOverflow = VerticalWrapMode.Overflow;
            Box(welcomeContent, "Title rule", 80, 372, 72, 5, Gold).GetComponent<Image>().raycastTarget = false;
            MenuText(welcomeContent, L("menu.tagline"), 80, 396, 620, 30, 20, Gold, semiboldFont, .1f);
            MenuAction(welcomeContent, "Menu Start", "01", "menu.start", 80, 500, 440, ShowUsername, MenuVariant.Primary);
            MenuAction(welcomeContent, "Menu Settings", "02", "menu.settings", 80, 588, 440, ShowSettings, MenuVariant.Secondary);
            MenuAction(welcomeContent, "Menu Quit", "03", "menu.quit", 80, 676, 440, QuitGame, MenuVariant.Quiet);
            MenuText(welcomeContent, L("menu.hint"), 80, 806, 640, 28, 14, Muted);
        }
        void BuildSkyline(Transform parent)
        {
            var random = new System.Random(314159);
            int x = 640, index = 0;
            while (x < 1600)
            {
                int width = random.Next(70, 131), height = random.Next(190, 521);
                width = Mathf.Min(width, 1600 - x);
                var building = Box(parent, "Skyline " + index, x, 900 - height, width, height, index % 2 == 0 ? Panel : Raised);
                building.GetComponent<Image>().raycastTarget = false;
                var roof = Box(building, "Roof light", 0, 0, width, 4, Alpha(Cyan, .45f)).GetComponent<Image>();
                roof.raycastTarget = false; menuLights.Add(roof);
                for (int wx = 14; wx + 8 < width; wx += 24)
                    for (int wy = 24; wy + 10 < height; wy += 30)
                        if (random.NextDouble() < .3)
                        {
                            var window = Box(building, "Window", wx, wy, 8, 10,
                                Alpha(random.NextDouble() < .7 ? Gold : Cyan, .85f)).GetComponent<Image>();
                            window.raycastTarget = false; menuLights.Add(window);
                        }
                x += width + random.Next(6, 21); index++;
            }
            ApplyMenuLightIntensity();
        }
        void ApplyMenuLightIntensity()
        {
            foreach (var light in menuLights)
                if (light != null) light.color = Alpha(light.color, (light.name == "Roof light" ? .45f : .85f) * VfxScale);
        }
        RectTransform FrontPanel(string name, float width, float height)
        {
            if (frontLayer != null) { frontLayer.SetActive(false); Destroy(frontLayer); }
            frontControls.Clear(); username = null;
            menuGroup.interactable = false; menuGroup.blocksRaycasts = false;
            frontLayer = Box(modal.transform, name + " scrim", 0, 0, 1600, 900, Alpha(Ink, .84f)).gameObject;
            Stretch((RectTransform)frontLayer.transform);
            var panel = Rounded(frontLayer.transform, name, 0, 0, width, height, Panel, 24, Alpha(Cyan, .3f)).rectTransform;
            Anchor(panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero);
            frontOpenedFrame = Time.frameCount;
            return panel;
        }
        public void ShowSettings()
        {
            if (modal == null || welcomeContent == null) ShowStart();
            var panel = FrontPanel("Settings", 720, 691); frontScreen = FrontScreen.Settings;
            MenuText(panel, L("settings.title"), 40, 36, 560, 46, 34, White, menuTitleFont);
            var close = Rounded(panel, "Settings Close", 636, 36, 44, 44, Raised, 10);
            var closeButton = close.gameObject.AddComponent<Button>(); closeButton.targetGraphic = close;
            closeButton.onClick.AddListener(BackToMenu); closeButton.navigation = new Navigation { mode = Navigation.Mode.None };
            MenuText(close.transform, L("icon.close"), 0, 0, 44, 44, 20, Muted, pixelFont, 0, TextAnchor.MiddleCenter);
            MenuText(panel, L("settings.audio_visual"), 40, 104, 640, 24, 13, Muted, semiboldFont, .16f);
            SettingSlider(panel, "SFX", "settings.sfx", 147, Mathf.RoundToInt(Game.effects.volume * 100), value => {
                Game.effects.volume = value / 100f; PlayerPrefs.SetFloat(SfxPreference, Game.effects.volume); PlayerPrefs.Save();
            });
            SettingSlider(panel, "VFX", "settings.vfx", 233, Mathf.RoundToInt(VfxIntensity), SetVfxIntensity);
            Box(panel, "Divider", 40, 325, 640, 1, Alpha(Muted, .2f)).GetComponent<Image>().raycastTarget = false;
            MenuText(panel, L("settings.language"), 40, 352, 640, 24, 13, Muted, semiboldFont, .16f);
            for (int i = 0; i < MenuLocale.Codes.Length; i++)
                LanguageOption(panel, i, 40 + i % 2 * 328, 396 + i / 2 * 80);
            MenuAction(panel, "Settings Back", L("key.escape"), "settings.back", 40, 579, 640, BackToMenu, MenuVariant.Secondary);
            frontControls.Add(closeButton); SelectControl(0);
        }
        void SettingSlider(Transform parent, string name, string key, float y, int initial, Action<float> changed)
        {
            var root = Rect(parent, name + " Slider", 40, y, 640, 72);
            MenuText(root, L(key), 0, 0, 540, 28, 15, Muted, semiboldFont, .14f);
            var valueText = MenuText(root, initial.ToString(), 550, 0, 90, 28, 22, Gold, pixelFont, 0, TextAnchor.MiddleRight);
            var hitArea = Rect(root, "Slider hit area", 0, 30, 640, 42);
            var hitGraphic = hitArea.gameObject.AddComponent<Image>(); hitGraphic.color = Alpha(Ink, 0);
            var track = Rounded(hitArea, "Track", 0, 14, 640, 12, Raised, 6); track.raycastTarget = false;
            var fill = Rounded(track.transform, "Fill", 0, 0, 640, 12, Cyan, 6); fill.raycastTarget = false;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var handleArea = Rect(track.transform, "Handle area", 0, 6, 640, 0);
            var thumb = Rounded(handleArea, "Thumb", 0, 0, 36, 36, White, 18, Ink, 4);
            thumb.rectTransform.pivot = new Vector2(.5f, .5f);
            thumb.rectTransform.anchoredPosition = Vector2.zero;
            thumb.raycastTarget = false;
            var slider = hitArea.gameObject.AddComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
            slider.fillRect = fill.rectTransform; slider.handleRect = thumb.rectTransform; slider.targetGraphic = thumb;
            slider.direction = Slider.Direction.LeftToRight;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.SetValueWithoutNotify(initial);
            slider.onValueChanged.AddListener(value => { valueText.text = Mathf.RoundToInt(value).ToString(); changed(value); });
            frontControls.Add(slider);
        }
        public void SetVfxIntensity(float value)
        {
            VfxIntensity = Mathf.Clamp(value, 0, 100);
            PlayerPrefs.SetFloat(VfxPreference, VfxIntensity); PlayerPrefs.Save(); ApplyMenuLightIntensity();
            if (Game?.areas != null) foreach (var area in Game.areas) area.RefreshVfx();
        }
        void LanguageOption(Transform parent, int index, float x, float y)
        {
            string code = MenuLocale.Codes[index]; bool selected = Language == code;
            var surface = Rounded(parent, "Language " + code, x, y, 312, 64, selected ? Raised : Panel, 16,
                Alpha(selected ? Cyan : Muted, selected ? 1 : .25f));
            var button = surface.gameObject.AddComponent<MenuButton>(); button.surface = surface;
            button.restingStroke = surface.stroke; button.targetGraphic = surface;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Rounded(surface.transform, "Radio", 18, 22, 20, 20, selected ? Cyan : Panel, 10,
                selected ? Cyan : Alpha(Muted, .6f)).raycastTarget = false;
            MenuText(surface.transform, MenuLocale.Names[index], 52, 0, 210, 64, 17, selected ? White : Muted, mediumFont);
            MenuText(surface.transform, code.ToUpperInvariant(), 264, 0, 30, 64, 13, selected ? Cyan : Muted, semiboldFont, .12f, TextAnchor.MiddleRight);
            button.onClick.AddListener(() => {
                Game.Play(Game.clickSound); Language = code;
                PlayerPrefs.SetString(LanguagePreference, code); PlayerPrefs.Save();
                foreach (Transform child in welcomeContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                BuildMenuContents(); ShowSettings(); SelectControl(2 + index);
            });
            frontControls.Add(button);
        }
        public void ShowUsername()
        {
            var panel = FrontPanel("Username", 640, 554); frontScreen = FrontScreen.Username;
            MenuText(panel, L("name.eyebrow"), 40, 36, 560, 24, 13, Cyan, semiboldFont, .16f);
            MenuText(panel, L("name.title"), 40, 78, 560, 64, 44, White, pixelFont, .02f);
            MenuText(panel, L("name.body"), 40, 152, 560, 46, 17, Muted);
            usernameSurface = Rounded(panel, "Username Field", 40, 208, 560, 64, Ink, 16, Cyan);
            username = usernameSurface.gameObject.AddComponent<InputField>();
            username.targetGraphic = usernameSurface; username.characterLimit = 16;
            username.lineType = InputField.LineType.SingleLine;
            username.navigation = new Navigation { mode = Navigation.Mode.None };
            var value = MenuText(usernameSurface.transform, "", 20, 18, 430, 28, 22, White, mediumFont);
            var hint = MenuText(usernameSurface.transform, L("name.placeholder"), 20, 18, 430, 28, 22, Muted, mediumFont);
            value.verticalOverflow = hint.verticalOverflow = VerticalWrapMode.Overflow;
            username.textComponent = value; username.placeholder = hint;
            username.customCaretColor = true; username.caretColor = Cyan; username.caretWidth = 3;
            var counter = MenuText(usernameSurface.transform, "", 456, 0, 84, 64, 14, Muted, semiboldFont, 0, TextAnchor.MiddleRight);
            usernameHelp = MenuText(panel, L("name.helper"), 40, 288, 560, 40, 14, Muted);
            username.onValueChanged.AddListener(text => {
                counter.text = string.Format(L("name.counter"), text.Length);
                usernameHelp.text = L("name.helper"); usernameHelp.color = Muted;
                usernameSurface.stroke = Cyan; usernameSurface.SetVerticesDirty();
            });
            username.text = PlayerIdentity.TryValidate(Game.Progress.playerName, out var clean, out _) ? clean : "";
            counter.text = string.Format(L("name.counter"), username.text.Length);
            frontControls.Add(username);
            MenuAction(panel, "Username Begin", L("key.enter"), "name.begin", 40, 344, 560, SubmitUsername, MenuVariant.Primary);
            MenuAction(panel, "Username Back", L("key.escape"), "name.back", 40, 442, 560, BackToMenu, MenuVariant.Quiet);
            SelectControl(0); username.ActivateInputField();
        }
        void SubmitUsername()
        {
            if (frontScreen != FrontScreen.Username) return;
            if (!Game.SetPlayerName(username.text, out string error))
            {
                usernameHelp.text = MenuLocale.ValidationError(error, Language); usernameHelp.color = Gold;
                usernameSurface.stroke = Gold; usernameSurface.SetVerticesDirty();
                username.ActivateInputField(); return;
            }
            transitionRoutine = StartCoroutine(GameplayTransition());
        }
        void BackToMenu()
        {
            var previous = frontScreen;
            if (frontLayer != null) { frontLayer.SetActive(false); Destroy(frontLayer); frontLayer = null; }
            username = null; frontScreen = FrontScreen.Main; frontOpenedFrame = Time.frameCount;
            menuGroup.interactable = true; menuGroup.blocksRaycasts = true;
            frontControls.Clear(); frontControls.AddRange(welcomeContent.GetComponentsInChildren<Button>());
            SelectControl(previous == FrontScreen.Settings ? 1 : 0);
        }
        IEnumerator GameplayTransition()
        {
            RemoveModal(); TakeNavigation(); frontScreen = FrontScreen.Transition;
            modal = Box(CanvasRoot, "Gameplay transition", 0, 0, 1600, 900, Ink).gameObject;
            Stretch((RectTransform)modal.transform);
            EventSystem.current?.SetSelectedGameObject(null);
            var card = Rect(modal.transform, "Welcome card", 0, 0, 1440, 280);
            Anchor(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero);
            var vignette = Rect(card, "Vignette", -80, -310, 1600, 900).gameObject.AddComponent<MenuGradient>();
            vignette.radial = true; vignette.color = Alpha(Raised, .55f * VfxScale); vignette.raycastTarget = false;
            MenuText(card, L("loading.eyebrow"), 0, 44, 1440, 26, 14, Cyan, semiboldFont, .2f, TextAnchor.MiddleCenter);
            var greeting = MenuText(card, L("loading.greeting"), 0, 92, 1440, 76, 52, White, pixelFont, .02f, TextAnchor.MiddleCenter);
            greeting.resizeTextForBestFit = true; greeting.resizeTextMinSize = 32; greeting.resizeTextMaxSize = 52;
            var track = Rounded(card, "Loading track", 480, 190, 480, 8, Raised, 4);
            var fill = Rounded(track.transform, "Loading progress", 0, 0, 0, 8, Cyan, 4);
            MenuText(card, L("loading.hint"), 0, 220, 1440, 30, 16, Muted, bodyFont, 0, TextAnchor.MiddleCenter);
            for (float elapsed = 0; elapsed < 1.2f; elapsed += Time.unscaledDeltaTime)
            {
                fill.rectTransform.sizeDelta = new Vector2(480 * Mathf.Clamp01(elapsed / 1.2f), 8);
                yield return null;
            }
            fill.rectTransform.sizeDelta = new Vector2(480, 8); yield return null;
            transitionOverlay = Box(CanvasRoot, "Gameplay fade", 0, 0, 1600, 900, Ink).gameObject;
            Stretch((RectTransform)transitionOverlay.transform);
            var fade = transitionOverlay.AddComponent<CanvasGroup>();
            RemoveModal(); Game.Begin(); TakeNavigation(); frontScreen = FrontScreen.Transition;
            for (float elapsed = 0; elapsed < .25f; elapsed += Time.unscaledDeltaTime)
            { fade.alpha = 1 - elapsed / .25f; yield return null; }
            Destroy(transitionOverlay); transitionOverlay = null; RestoreNavigation(); frontScreen = FrontScreen.None;
            transitionRoutine = null;
        }
        void CancelTransition()
        {
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = null;
            if (transitionOverlay != null) { transitionOverlay.SetActive(false); Destroy(transitionOverlay); transitionOverlay = null; }
        }
        void SelectControl(int index)
        {
            if (frontControls.Count == 0) return;
            var control = frontControls[(index + frontControls.Count) % frontControls.Count];
            EventSystem.current?.SetSelectedGameObject(control.gameObject);
        }
        void Update()
        {
            if (!FrontEndActive || frontScreen == FrontScreen.Transition || Time.frameCount <= frontOpenedFrame) return;
            var keyboard = Keyboard.current; if (keyboard == null || EventSystem.current == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            { if (frontScreen != FrontScreen.Main) BackToMenu(); return; }
            int selected = frontControls.FindIndex(c => c != null && c.gameObject == EventSystem.current.currentSelectedGameObject);
            bool editing = username != null && username.isFocused;
            if (keyboard.tabKey.wasPressedThisFrame || (!editing && (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)))
                SelectControl(selected + (keyboard.shiftKey.isPressed ? -1 : 1));
            else if (!editing && (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)) SelectControl(selected - 1);
            if (selected >= 0 && frontControls[selected] is Slider slider)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) slider.value -= 1;
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) slider.value += 1;
            }
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                if (frontScreen == FrontScreen.Username && (editing || selected <= 0)) { Game.Play(Game.clickSound); SubmitUsername(); }
                else if (selected >= 0 && frontControls[selected] is Button button) button.onClick.Invoke();
            }
        }
        void QuitGame()
        {
            Game.Save(); PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        void OnDestroy()
        {
            CancelTransition(); RestoreNavigation();
            if (CanvasRoot != null) Destroy(CanvasRoot.gameObject);
        }

        public void ShowBriefing(Mission mission)
        {
            var c = OpenModal(mission.area + " / " + mission.npc, mission.title, "MISSION " + (Game.Progress.completed + 1).ToString("00") + "   /   UP TO 150 XP");
            Text(c, mission.briefing, 0, 4, 1010, 130, 23, White);
            var task = Box(c, "Assignment", 0, 160, 1056, 150, Raised);
            Text(task, "YOUR ASSIGNMENT", 24, 20, 1000, 24, 12, Cyan, true);
            Text(task, mission.objective, 24, 57, 1000, 79, 21, White);
            Text(c, "Fictional evidence. No external connections. Mistakes include an explanation and a retry.", 0, 340, 1020, 55, 17, Muted);
            Button(c, "START MISSION   >", 0, 416, 370, 58, () => modules.Start(mission), Cyan);
            Button(c, "Return to the city", 390, 416, 300, 58, CloseModal);
        }

        public void ShowMessage(string title, string message, string button)
        {
            var c = OpenModal("CITIZEN NETWORK", title);
            Text(c, message, 0, 24, 1000, 260, 24);
            Button(c, button, 0, 410, 420, 60, CloseModal, Cyan);
        }
        public void ShowDebrief(Mission mission, int xp)
        {
            var c = OpenModal("MISSION COMPLETE / " + mission.area, "Connection secured.", "+" + xp + " XP  /  " + Game.Progress.completed + " of " + Game.Campaign.missions.Length + " missions complete");
            Text(c, mission.takeaway, 0, 18, 1020, 150, 25);
            Text(c, Game.Current == null ? "{player}, you completed the Citizen Security Program." : "NEXT ASSIGNMENT FOR {player}\n\n" + Game.Current.title + "\nFind " + Game.Current.npc + " at " + Game.Current.area + ".", 0, 205, 1000, 160, 21, Teal);
            Button(c, Game.Current == null ? "VIEW PATROL REPORT   >" : "BACK TO THE CITY   >", 0, 414, 440, 60,
                () => { if (Game.Current == null) ShowEnding(); else CloseModal(); }, Cyan);
        }
        public void ShowEnding()
        {
            var c = OpenModal("CITIZEN SECURITY PROGRAM / COMPLETE", "Well done, {player}.", "PATROL REPORT  /  ALL DISTRICTS SECURED");
            PatrolReport(c);
            Button(c, "EXPLORE THE CITY", 0, 414, 350, 60, CloseModal, Cyan);
            Button(c, "Review field guide", 370, 414, 350, 60, ShowHelp);
        }
        public void ShowGraduation(Action pledge)
        {
            var c = OpenModal("CYBER ACADEMY / GRADUATION", "Your progress, {player}.", "Review your earned XP and area badges, then take the final security pledge.");
            PatrolReport(c);
            Button(c, "TAKE THE FINAL PLEDGE", 0, 414, 480, 60, pledge, Cyan);
        }
        void PatrolReport(RectTransform c)
        {
            Text(c, Game.Progress.score + " / " + ProgressStore.MaximumScore(Game.Campaign) + " XP", 0, 0, 1000, 78, 54, Gold, true);
            Text(c, "{player}, your patrol joins evidence analysis, three Linux investigations, browser patch verification and SOC incident response.", 0, 86, 1030, 56, 20);
            string[] areas = { "Cyber Academy", "Internet Cafe", "Linux Lab", "Web Security Lab" };
            string[] badges = { "TRUST BUILDER", "EVIDENCE ANALYST", "FILESYSTEM INVESTIGATOR", "APPLICATION & SOC DEFENDER" };
            for (int i = 0; i < areas.Length; i++)
            {
                bool earned = Game.Campaign.missions.Select((m, index) => new { m.area, index }).Where(m => m.area == areas[i]).All(m => m.index < Game.Progress.completed);
                var badge = Box(c, "Badge / " + areas[i], i % 2 * 538, 158 + i / 2 * 85, 518, 73, Raised);
                Text(badge, (earned ? "EARNED / " : "IN PROGRESS / ") + areas[i], 15, 10, 490, 24, 14, earned ? Teal : Gold, true);
                Text(badge, badges[i], 15, 39, 490, 24, 16, White, true);
            }
            Text(c, "YOUR FIELD RULE: Pause. Verify independently. Share less. Report early.", 0, 347, 1050, 50, 19, Teal);
        }
        public void ShowJournal()
        {
            var c = OpenModal("FIELD JOURNAL", "Your learning route", "Complete assignments in order to unlock each district and its simulations.");
            int rows = (Game.Campaign.missions.Length + 1) / 2;
            var journal = Scroll(c, "Mission list", 0, 0, 1056, 425, rows * 70);
            for (int i = 0; i < Game.Campaign.missions.Length; i++)
            {
                int col = i / rows, row = i % rows; var m = Game.Campaign.missions[i];
                var line = Box(journal, m.id, col * 537, row * 70, 518, 60, Raised);
                Color color = i < Game.Progress.completed ? Teal : i == Game.Progress.completed ? Gold : Muted;
                Text(line, i < Game.Progress.completed ? "OK" : (i + 1).ToString("00"), 13, 16, 35, 27, 16, color, true);
                Text(line, m.title, 60, 8, 446, 27, 18, color, true);
                Text(line, m.npc + " / " + m.area, 60, 36, 446, 20, 12, Muted);
            }
            Button(c, "Return to the city", 0, 435, 350, 48, CloseModal, Cyan);
        }
        public void ShowMap()
        {
            var c = OpenModal("CITY ATLAS", "Four districts. One connected city.", "Gold marker = current mentor. Walk to them and press E to begin.");
            var p = Rect(c, "Map image", 0, -2, 620, 486).gameObject.AddComponent<Image>(); p.sprite = cityPreview; p.preserveAspect = true;
            var destination = Game.Current == null ? null : Game.areas.FirstOrDefault(a => a.areaName == Game.Current.area);
            if (destination != null)
            {
                Vector3 door = destination.door.position;
                float x = door.x / WorldLayout.Width * 620;
                float y = (486 - 348.75f) * .5f - 2 - door.y / WorldLayout.Height * 348.75f;
                var marker = Text(c, "!", x - 14, y - 30, 28, 34, 30, Gold, true);
                marker.name = "Active mission building"; marker.alignment = TextAnchor.MiddleCenter;
                marker.gameObject.AddComponent<Outline>().effectColor = Ink;
            }
            string[] area = { "Cyber Academy", "Internet Cafe", "Linux Lab", "Web Security Lab" };
            string[] pos = { "NORTH / Professor Byte", "WEST / Alex, Maya, Detective Byte", "NORTHEAST / SysAdmin Tux", "EAST / Red and Blue" };
            for (int i = 0; i < 4; i++)
            {
                bool open = Game.Progress.completed >= CampaignLoader.UnlockForArea(Game.Campaign, area[i]);
                Text(c, area[i], 658, i * 82, 390, 30, 23, open ? Cyan : Muted, true);
                Text(c, pos[i] + (open ? " / OPEN" : " / LOCKED"), 658, 37 + i * 82, 390, 36, 13, Muted);
            }
            Text(c, Game.Current == null ? "All missions complete." : "CURRENT: " + Game.Current.npc + "\n" + Game.Current.title, 658, 345, 390, 70, 19, Gold);
            Button(c, "Back to exploring", 658, 432, 390, 50, CloseModal, Cyan);
        }
        public void ShowHelp()
        {
            var c = OpenModal("POCKET FIELD GUIDE", "Think before you click.", "Keep these habits with you beyond CyberCity.");
            string[] titles = { "01  VERIFY THE SOURCE", "02  PROTECT YOUR ACCOUNT", "03  INVESTIGATE SAFELY", "04  BUILD IN DEFENSE" };
            string[] body = {
                "Read the full sender address and domain. Urgency and rewards can be bait. Open the official app or a saved bookmark to verify.",
                "Use unique passwords and a password manager. Enable MFA. Never share verification codes. Report suspicious requests.",
                "Linux: pwd shows your location; ls -a reveals hidden files; cd changes folder; cat reads a file. Type help in the virtual terminal.",
                "IDOR: check ownership on the server. XSS: encode output for its context. SQLi: bind values using parameterized queries. Patch and monitor." };
            for (int i = 0; i < 4; i++)
            {
                int col = i % 2, row = i / 2;
                Text(c, titles[i], col * 540, row * 183, 505, 30, 17, Cyan, true);
                Text(c, body[i], col * 540, 44 + row * 183, 505, 118, 20, White);
            }
            Button(c, "BACK TO THE CITY", 0, 420, 390, 56, CloseModal, Cyan);
        }
        public void ShowPause()
        {
            Game.Save();
            var c = OpenModal("PATROL PAUSED", "Take a breath. Stay curious.", "Progress is saved locally after every mission and every 15 seconds.");
            Button(c, "Resume patrol", 0, 16, 480, 58, CloseModal, Cyan);
            Button(c, "Field guide", 0, 92, 480, 58, ShowHelp);
            Button(c, "Mission journal", 0, 168, 480, 58, ShowJournal);
            Button(c, "Restart patrol", 0, 244, 480, 58, () => {
                var r = OpenModal("NEW PATROL", "Start again from mission one?", "This replaces your saved mission progress and score on this device.");
                Button(r, "Keep my progress", 0, 360, 430, 60, CloseModal, Cyan);
                Button(r, "Restart from the beginning", 460, 360, 500, 60, Game.NewPatrol);
            });
            Button(c, "Save and quit", 0, 320, 480, 58, () => {
                Game.Save();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
            Text(c, "AMBIENCE", 550, 23, 450, 30, 15, Muted, true);
            Button(c, Game.music.volume > 0 ? "Music: ON" : "Music: OFF", 550, 70, 450, 56, () => {
                Game.music.volume = Game.music.volume > 0 ? 0 : .32f; PlayerPrefs.SetFloat("CyberCityMusic", Game.music.volume); PlayerPrefs.Save(); ShowPause();
            });
            Button(c, Game.effects.volume > 0 ? "Sound effects: ON" : "Sound effects: OFF", 550, 148, 450, 56, () => {
                Game.effects.volume = Game.effects.volume > 0 ? 0 : .7f; PlayerPrefs.SetFloat("CyberCityEffects", Game.effects.volume); PlayerPrefs.Save(); ShowPause();
            });
            Button(c, "Change username", 550, 226, 450, 56, ShowRename);
            Text(c, "WASD / Arrow keys    Move\nE    Talk to a nearby mentor\nJ    Journal     M    Map\nH    Field guide     ESC    Close / pause", 550, 310, 480, 150, 20, Muted);
        }
        void ShowRename()
        {
            var c = OpenModal("PATROL IDENTITY", "Change your username", "Your mission progress stays with you, {player}.");
            var input = Input(c, 0, 36, 650, 56, "New username"); input.text = Game.Progress.playerName; input.characterLimit = 64;
            var error = Text(c, "Use 3–16 letters, numbers, spaces or underscore.", 0, 112, 1010, 60, 20, Muted);
            Button(c, "SAVE USERNAME", 0, 220, 420, 58, () => {
                if (!Game.SetPlayerName(input.text, out string reason)) { error.text = reason; error.color = Gold; return; }
                ShowPause(); Toast("Patrol identity updated, {player}.");
            }, Cyan);
        }
        public void ShowFatal(string error)
        {
            OpenModal("CONTENT ERROR", "The patrol could not load.", error);
        }
    }
}
