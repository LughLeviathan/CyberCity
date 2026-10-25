using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CyberCity
{
    public sealed class GameUI : MonoBehaviour
    {
        public Sprite cityPreview;
        public static readonly Color Ink = Hex("090F22"), Panel = Hex("101A33"), Raised = Hex("1B2947"),
            Cyan = Hex("00E5FF"), Gold = Hex("FFB347"), Teal = Hex("00D4B7"), Violet = Hex("8B5CF6"),
            White = Hex("FFF4D6"), Muted = Hex("8EA5C5"), Pink = Hex("FF2D9B");
        public CyberCityGame Game { get; private set; }
        public RectTransform CanvasRoot { get; private set; }
        public RectTransform Content { get; private set; }
        public bool HasModal => modal != null;
        Font font;
        GameObject modal;
        RectTransform hud, labels;
        Text objective, objectiveBody, score, count, prompt, toast, status;
        Image[] segments;
        Text[] districtStates;
        readonly Dictionary<Citizen, Text> nameLabels = new Dictionary<Citizen, Text>();
        readonly Dictionary<Citizen, Text> markers = new Dictionary<Citizen, Text>();
        Text playerLabel;
        float toastUntil;
        ModuleUI modules;

        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color c); return c; }
        void EnsureCanvas()
        {
            if (CanvasRoot != null) return;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("CyberCity Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            CanvasRoot = (RectTransform)go.transform;
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
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
            if (CanvasRoot != null) Destroy(CanvasRoot.gameObject);
            CanvasRoot = null; modal = null; Content = null;
            nameLabels.Clear(); markers.Clear();
            Game = game; EnsureCanvas(); modules = new ModuleUI(this);
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
            BuildHud(); ShowStart();
        }

        void BuildHud()
        {
            hud = Rect(CanvasRoot, "Patrol HUD", 0, 0, 1600, 900);
            var top = Box(hud, "Header", 0, 0, 1600, 86, Ink);
            Box(top, "Accent", 28, 24, 5, 38, Cyan);
            Text(top, "CYBERCITY", 48, 16, 260, 34, 29, White, true);
            Text(top, "THINK BEFORE YOU CLICK", 49, 51, 320, 22, 12, Muted);
            Text(top, "CITIZEN SECURITY PROGRAM", 550, 25, 360, 25, 14, Muted);
            status = Text(top, CampaignLoader.Status, 550, 49, 360, 20, 11, Teal);
            score = Text(top, "", 1050, 26, 195, 32, 22, Gold, true);
            Button(top, "J  Journal", 1270, 22, 146, 42, ShowJournal);
            Button(top, "ESC  Menu", 1428, 22, 144, 42, ShowPause);
            Box(hud, "Header rule", 0, 85, 1600, 1, Raised);
            var side = Box(hud, "Mission console", 1262, 106, 310, 746, Panel);
            Text(side, "YOUR PATROL", 24, 22, 250, 24, 13, Cyan, true);
            count = Text(side, "", 24, 57, 260, 32, 25, White, true);
            segments = new Image[Game.Campaign.missions.Length];
            float segmentWidth = 262f / segments.Length;
            for (int i = 0; i < segments.Length; i++) segments[i] = Box(side, "Mission " + i, 24 + i * segmentWidth, 105, segmentWidth * .78f, 5, Raised).GetComponent<Image>();
            Text(side, "CURRENT OBJECTIVE", 24, 142, 270, 22, 12, Muted, true);
            objective = Text(side, "", 24, 178, 268, 72, 24, White, true);
            objectiveBody = Text(side, "", 24, 258, 264, 100, 17, Muted);
            Box(side, "Divider", 24, 374, 262, 1, Raised);
            Text(side, "CITY DISTRICTS", 24, 400, 260, 24, 12, Muted, true);
            string[] areas = { "Cyber Academy", "Internet Cafe", "Linux Lab", "Web Security Lab" };
            districtStates = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                Text(side, "0" + (i + 1), 24, 445 + i * 48, 30, 26, 14, Cyan, true);
                Text(side, areas[i], 65, 442 + i * 48, 230, 24, 16, White);
                districtStates[i] = Text(side, "", 65, 464 + i * 48, 230, 16, 9, Muted);
            }
            Button(side, "M  City map", 24, 670, 262, 48, ShowMap);
            var foot = Box(hud, "Controls", 0, 864, 1600, 36, Ink);
            Text(foot, "W A S D   Move      E   Talk / interact      J   Journal      M   Map      H   Field guide", 28, 9, 1100, 24, 13, Muted);
            Text(foot, "FICTIONAL SYSTEMS / LOCAL ONLY", 1280, 10, 300, 22, 11, Teal);
            var location = Box(hud, "Location", 28, 108, 290, 42, Panel);
            Text(location, "SECTOR 01  /  THE COMMONS", 16, 12, 265, 22, 13, Cyan, true);
            prompt = Text(hud, "", 330, 793, 610, 43, 22, Gold, true); prompt.alignment = TextAnchor.MiddleCenter;
            toast = Text(hud, "", 34, 838, 1200, 25, 15, Teal);
            RefreshHud();
        }

        public void RefreshHud()
        {
            if (Game == null || count == null) return;
            playerLabel.text = Game.Personalize("{player}");
            count.text = Game.Progress.completed.ToString("00") + " / " + Game.Campaign.missions.Length + " complete";
            score.text = Game.Progress.score.ToString("0000") + "  XP";
            for (int i = 0; i < segments.Length; i++) segments[i].color = i < Game.Progress.completed ? Teal : i == Game.Progress.completed ? Gold : Raised;
            string[] areas = { "Cyber Academy", "Internet Cafe", "Linux Lab", "Web Security Lab" };
            for (int i = 0; i < 4; i++)
            {
                int unlock = CampaignLoader.UnlockForArea(Game.Campaign, areas[i]);
                bool open = Game.Progress.completed >= unlock;
                districtStates[i].text = open ? "ACCESS GRANTED" : "COMPLETE MISSION " + unlock.ToString("00") + " TO UNLOCK";
                districtStates[i].color = open ? Teal : Muted;
            }
            objective.text = Game.Personalize(Game.Current == null ? "City secured." : Game.Current.title);
            objectiveBody.text = Game.Personalize(Game.Current == null ? "All missions complete. Your field guide is always available." :
                "Find " + Game.Current.npc + "\n" + Game.Current.area + "\n\nApproach and press E.");
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
                PositionLabel(nameLabels[n], n.transform.position + Vector3.down * .12f);
                markers[n].gameObject.SetActive(n == Game.Target);
                PositionLabel(markers[n], n.transform.position + Vector3.up * (1.25f + Mathf.Sin(Time.time * 3) * .08f));
            }
            PositionLabel(playerLabel, Game.player.transform.position + Vector3.up * 1.05f);
            prompt.text = !HasModal && Game.Nearest != null ? "[ E ]  Talk to " + Game.Nearest.citizenName : "";
            if (Time.unscaledTime > toastUntil) toast.text = "";
        }

        public void Toast(string message) { if (toast != null) { toast.text = Game.Personalize(message); toastUntil = Time.unscaledTime + 6; } }

        public void CloseModal()
        {
            if (Game != null && !Game.Started) return;
            if (modal != null) { modal.SetActive(false); Destroy(modal); modal = null; Content = null; }
            EventSystem.current?.SetSelectedGameObject(null);
        }
        void RemoveModal()
        {
            if (modal != null) { modal.SetActive(false); Destroy(modal); }
            modal = null;
        }
        public RectTransform OpenModal(string kicker, string title, string subtitle = "")
        {
            EnsureCanvas(); RemoveModal();
            modal = Box(CanvasRoot, "Modal scrim", 0, 0, 1600, 900, new Color(.015f,.025f,.065f,.94f)).gameObject;
            var panel = Box(modal.transform, "Module panel", 240, 104, 1120, 696, Panel);
            Box(panel, "Top accent", 0, 0, 1120, 3, Cyan);
            Text(panel, kicker.ToUpperInvariant(), 32, 25, 1010, 23, 12, Cyan, true);
            Text(panel, title, 32, 58, 990, 49, 34, White, true);
            Text(panel, subtitle, 32, 113, 1020, 48, 17, Muted);
            Button(panel, "X", 1040, 22, 48, 40, CloseModal);
            Content = Rect(panel, "Module content", 32, 170, 1056, 492);
            return Content;
        }

        public void ShowStart()
        {
            RemoveModal();
            modal = Box(CanvasRoot, "Welcome", 0, 0, 1600, 900, Ink).gameObject;
            var picture = Rect(modal.transform, "City illustration", 600, 48, 1015, 798).gameObject.AddComponent<Image>();
            picture.sprite = cityPreview; picture.preserveAspect = true; picture.color = new Color(.7f,.8f,1,.67f); picture.raycastTarget = false;
            Box(modal.transform, "Title back", 0, 0, 660, 900, new Color(.035f,.059f,.133f,.96f));
            Text(modal.transform, "S I J A W   /   FIELD OPERATIONS", 80, 114, 560, 30, 14, Cyan, true);
            Text(modal.transform, "CYBER\nCITY", 72, 187, 570, 250, 106, White, true);
            Box(modal.transform, "Title rule", 80, 446, 70, 5, Gold);
            Text(modal.transform, "THINK BEFORE YOU CLICK", 80, 482, 560, 40, 22, Gold, true);
            Text(modal.transform, "A city of connections. A world of hidden risks.\nBuild the skills to protect it, {player}.", 80, 538, 510, 65, 20, Muted);
            bool returning = Game.Progress.started && PlayerIdentity.TryValidate(Game.Progress.playerName, out _, out _);
            if (returning)
            {
                Text(modal.transform, "WELCOME BACK, {player}", 80, 617, 510, 30, 20, Teal, true);
                Button(modal.transform, "CONTINUE PATROL   >", 80, 673, 440, 64, Game.Begin, Cyan);
            }
            else
            {
                var name = Input(modal.transform, 80, 620, 440, 48, "Choose your username (3–16 characters)");
                name.characterLimit = 64; name.text = Game.Progress.playerName;
                var error = Text(modal.transform, "Letters, numbers, spaces and underscore.", 80, 677, 510, 36, 15, Muted);
                Button(modal.transform, "BEGIN YOUR PATROL   >", 80, 723, 440, 64, () => {
                    if (!Game.SetPlayerName(name.text, out string reason)) { error.text = reason; error.color = Gold; return; }
                    Game.Begin();
                }, Cyan);
                name.ActivateInputField();
            }
            Text(modal.transform, Game.Campaign.missions.Length + " MISSIONS    /    " + Game.citizens.Length + " MENTORS    /    4 DISTRICTS", 80, 805, 590, 26, 13, Muted);
            var card = Box(modal.transform, "Orientation", 1002, 668, 510, 150, Panel);
            Text(card, "YOUR FIRST ASSIGNMENT", 24, 19, 460, 24, 12, Cyan, true);
            Text(card, Game.Current == null ? "Patrol complete. Welcome back." : "Meet " + Game.Current.npc, 24, 52, 460, 34, 24, White, true);
            Text(card, "Explore with WASD. Talk with E. Learn by doing.", 24, 103, 460, 26, 16, Muted);
            Text(modal.transform, ProgressStore.LastError ?? CampaignLoader.Status + "   /   PROGRESS SAVED ON THIS DEVICE", 80, 848, 1400, 40, 12, Teal);
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
