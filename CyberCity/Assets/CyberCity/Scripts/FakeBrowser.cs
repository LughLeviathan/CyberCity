using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace CyberCity
{
    // uGUI-only browser chrome. All request/response data comes from WebLabSession.
    public sealed class FakeBrowser
    {
        readonly GameUI ui;
        readonly Mission mission;
        readonly WebScenario scenario;
        readonly WebLabSession session;
        readonly Action completed;
        readonly List<string> history = new List<string>();
        int historyIndex = -1;
        bool codeEnabled;
        InputField address, form, password;
        Text output, network, stage, guidance;
        Button back, forward, patch, enable, finish;
        RectTransform networkContent;

        public FakeBrowser(GameUI ui, Mission mission, WebScenario scenario, WebProgress state, Action completed)
        { this.ui = ui; this.mission = mission; this.scenario = scenario; session = new WebLabSession(scenario, state); this.completed = completed; }

        public void Show()
        {
            var c = ui.OpenModal("WEB SECURITY LAB / FICTIONAL BROWSER", mission.title, "Demonstrate the flaw. Enable the source fix. Replay the same request and inspect the result.");
            var browser = ui.Box(c, "Fake browser", 0, 0, 710, 492, GameUI.Ink);
            ui.Box(browser, "Browser tab", 10, 4, 470, 28, GameUI.Raised);
            ui.Text(browser, scenario.title + "  /  LOCAL SIMULATION", 22, 9, 650, 22, 13, GameUI.Cyan, true);
            back = ui.Button(browser, "<", 10, 40, 44, 42, () => Travel(-1));
            forward = ui.Button(browser, ">", 60, 40, 44, 42, () => Travel(1));
            ui.Button(browser, "Reload", 110, 40, 90, 42, () => Navigate(false)).GetComponentInChildren<Text>().fontSize = 14;
            address = ui.Input(browser, 208, 40, 422, 42, "Edit the fictional URL"); address.characterLimit = 512; address.gameObject.name = "Browser address";
            address.text = scenario.StartUrl;
            address.onEndEdit.AddListener(_ => { if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) Navigate(); });
            ui.Button(browser, "GO", 638, 40, 62, 42, () => Navigate());
            var page = ui.Box(browser, "Simulated page", 10, 94, 690, 242, GameUI.Raised);
            ui.Text(page, scenario.title, 14, 9, 660, 28, 21, GameUI.White, true);
            ui.Text(page, scenario.description, 14, 44, 660, 45, 16, GameUI.Muted);
            if (scenario.kind != "idor")
            {
                form = ui.Input(page, 14, 94, scenario.kind == "sqli" ? 254 : 480, 44, scenario.kind == "xss" ? "Write a training comment" : "Fictional username");
                form.gameObject.name = "Browser form"; form.characterLimit = 512;
                if (scenario.kind == "sqli")
                {
                    password = ui.Input(page, 280, 94, 214, 44, "Fictional password");
                    password.gameObject.name = "Browser password"; password.contentType = InputField.ContentType.Password; password.text = "training";
                }
                ui.Button(page, scenario.kind == "xss" ? "POST" : "SIGN IN", 510, 94, 166, 44, () => Navigate());
            }
            var response = ui.Scroll(page, "Page response", 14, scenario.kind == "idor" ? 100 : 148, 660, scenario.kind == "idor" ? 130 : 84, 240);
            response.GetComponentInParent<ScrollRect>().GetComponent<Image>().color = GameUI.Raised;
            output = ui.Text(response, "", 0, 0, 648, 240, 17, GameUI.Teal);
            var devtools = ui.Box(browser, "DevTools Network", 10, 386, 690, 102, GameUI.Panel);
            networkContent = ui.Scroll(devtools, "Network request and response", 8, 5, 674, 92, 360);
            network = ui.Text(networkContent, "", 0, 0, 660, 360, 15, GameUI.Cyan);
            devtools.gameObject.SetActive(false);
            ui.Button(browser, "DevTools / Network", 10, 344, 285, 34, () => devtools.gameObject.SetActive(!devtools.gameObject.activeSelf));
            Button hint = null;
            hint = ui.Button(browser, "Hint (-5 XP)", 430, 344, 270, 34, () => {
                if (session.State.hints >= scenario.hints.Length) return;
                guidance.text = scenario.hints[session.State.hints++];
                ui.Game.Progress.hintUses[ui.Game.Progress.completed]++; ui.Game.Save();
                hint.interactable = session.State.hints < scenario.hints.Length;
            });
            hint.interactable = session.State.hints < scenario.hints.Length && !session.State.verified;
            stage = ui.Text(c, "", 734, 0, 320, 30, 16, GameUI.Gold, true);
            guidance = ui.Text(c, "", 734, 39, 320, 75, 17, GameUI.White);
            ui.Text(c, "VULNERABLE SOURCE", 734, 117, 320, 25, 12, GameUI.Muted, true);
            var vulnerable = ui.Box(c, "Vulnerable code", 734, 146, 322, 66, GameUI.Ink);
            ui.Text(vulnerable, scenario.vulnerableCode, 10, 8, 302, 54, 15, GameUI.Gold);
            ui.Text(c, "BLUE TEAM PATCH", 734, 224, 320, 25, 12, GameUI.Cyan, true);
            var fixedCode = ui.Box(c, "Patch source", 734, 255, 322, 77, GameUI.Ink);
            ui.Text(fixedCode, scenario.patchedCode, 10, 8, 302, 64, 15, GameUI.Teal);
            codeEnabled = session.State.patched;
            enable = ui.Button(c, "ENABLE FIX: " + scenario.patchLabel, 734, 344, 322, 40, () => {
                codeEnabled = !codeEnabled; RefreshStage();
            });
            enable.GetComponentInChildren<Text>().fontSize = 14;
            patch = ui.Button(c, "APPLY PATCH", 734, 392, 322, 40, () => {
                if (codeEnabled && session.ApplyPatch()) { ui.Game.Save(); RefreshStage(); }
            }, GameUI.Cyan);
            finish = ui.Button(c, "RETEST SAME REQUEST", 734, 440, 322, 48, () => {
                if (session.State.verified) { completed(); return; }
                address.text = session.State.attackUrl; if (form != null) form.text = session.State.attackInput;
                if (password != null) password.text = session.State.attackPassword; Navigate();
            }, GameUI.Cyan);
            finish.GetComponentInChildren<Text>().fontSize = 15;
            Navigate();
        }
        void Travel(int delta)
        {
            int next = historyIndex + delta; if (next < 0 || next >= history.Count) return;
            historyIndex = next; address.text = history[next]; Navigate(false);
        }
        void Navigate(bool record = true)
        {
            var response = session.Request(address.text, form == null ? "" : form.text, password == null ? "training" : password.text);
            output.text = ui.Game.Personalize(response.page); output.color = response.fakeAlert ? GameUI.Gold : GameUI.Teal;
            network.text = response.network;
            if (record)
            {
                if (historyIndex < history.Count - 1) history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
                history.Add(address.text); historyIndex = history.Count - 1;
            }
            back.interactable = historyIndex > 0; forward.interactable = historyIndex < history.Count - 1;
            ui.Game.Save(); RefreshStage();
        }
        void RefreshStage()
        {
            var s = session.State;
            stage.text = s.verified ? "03 / PATCH VERIFIED" : s.patched ? "03 / REPLAY THE ATTACK" : s.demonstrated ? "02 / BLUE TEAM PATCH" : "01 / OBSERVE THE FLAW";
            guidance.text = s.verified ? "The captured attack is now safe. Review the debrief to learn why." : s.patched ? "Run the same URL and input again. A different request cannot verify this fix." : s.demonstrated ? "Attack captured. Enable the source fix and apply the patch." : "Inspect the page and change its URL or form. DevTools shows fictional requests.";
            enable.interactable = s.demonstrated && !s.patched;
            enable.GetComponentInChildren<Text>().text = (codeEnabled ? "[ON] " : "ENABLE FIX: ") + scenario.patchLabel;
            patch.interactable = s.demonstrated && codeEnabled && !s.patched;
            finish.interactable = s.patched;
            finish.GetComponentInChildren<Text>().text = s.verified ? "CONTINUE TO DEBRIEF" : "RETEST SAME REQUEST";
        }
    }
}
