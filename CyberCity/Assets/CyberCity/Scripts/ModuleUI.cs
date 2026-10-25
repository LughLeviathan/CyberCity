using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CyberCity
{
    public sealed class ModuleUI
    {
        readonly GameUI ui;
        CyberCityGame game => ui.Game;
        Mission mission;
        int questionIndex;
        bool finished;
        public ModuleUI(GameUI owner) { ui = owner; }

        public void Start(Mission value)
        {
            mission = value; questionIndex = 0; finished = false;
            switch (mission.kind)
            {
                case "evidence": ShowEvidence(); break;
                case "terminal": ShowTerminal(); break;
                case "idor": case "xss": case "sqli": ShowWebLab(); break;
                case "soc": new SocUI(ui, mission, game.SocState(mission), Finish).Show(); break;
                case "final": ShowFinal(); break;
                case "graduation": ui.ShowGraduation(ShowQuestion); break;
                default: ShowQuestion(); break;
            }
        }

        void Finish()
        {
            if (finished || game.Current != mission) return;
            finished = true; game.CompleteMission();
        }

        void ShowQuestion()
        {
            if (mission.questions == null || questionIndex >= mission.questions.Length) { Finish(); return; }
            var q = mission.questions[questionIndex];
            var c = ui.OpenModal(mission.npc + " / FIELD EXERCISE", mission.title,
                "DECISION " + (questionIndex + 1) + " / " + mission.questions.Length + "  /  Read the situation, then choose your response.");
            ui.Text(c, q.prompt, 0, 0, 1030, 104, 25, GameUI.White, true);
            var buttons = new List<Button>(); bool answered = false;
            var feedback = ui.Text(c, "", 0, 347, 1035, 66, 18, GameUI.Muted);
            for (int i = 0; i < q.options.Length; i++)
            {
                int option = i;
                buttons.Add(ui.Button(c, ((char)('A' + i)) + "   " + q.options[i], 0, 113 + i * 70, 1056, 60, () => {
                    if (answered) return;
                    if (option == q.answer)
                    {
                        answered = true;
                        foreach (var b in buttons) b.interactable = false;
                        feedback.text = "CORRECT  /  " + game.Personalize(q.explanation); feedback.color = GameUI.Teal;
                        game.Play(game.successSound);
                        ui.Button(c, questionIndex == mission.questions.Length - 1 ? "COMPLETE MISSION   >" : "NEXT DECISION   >", 0, 424, 480, 56,
                            () => { questionIndex++; ShowQuestion(); }, GameUI.Cyan);
                    }
                    else
                    {
                        game.WrongAnswer(); buttons[option].interactable = false;
                        feedback.text = "TRY AGAIN  /  " + game.Personalize(q.explanation); feedback.color = GameUI.Gold;
                    }
                }));
            }
        }

        void ShowEvidence()
        {
            var marked = new HashSet<int>();
            var c = ui.OpenModal(mission.kind == "evidence" && mission.sender != "" ? "EVIDENCE ANALYZER / MESSAGE" : "EVIDENCE ANALYZER / FICTIONAL BROWSER",
                mission.title, "Inspect the evidence. Highlight every suspicious indicator, then submit your verdict.");
            var document = ui.Box(c, "Evidence document", 0, 0, 650, 412, GameUI.Raised);
            ui.Text(document, mission.subject, 20, 17, 610, 35, 23, GameUI.White, true);
            ui.Text(document, string.IsNullOrEmpty(mission.sender) ? "URL  " + mission.address : "FROM  " + mission.sender, 20, 62, 610, 38, 16, GameUI.Cyan);
            ui.Text(document, mission.document, 20, 106, 610, 60, 18, GameUI.Muted);
            var detail = ui.Text(c, "SELECT AN INDICATOR\n\nClick a line on the left to inspect it. Gold highlights mark evidence you identified.\n\nSome details are harmless. Find the combination that reveals the risk.", 680, 13, 365, 266, 21, GameUI.White);
            var progress = ui.Text(c, "0 indicators highlighted", 680, 309, 370, 35, 18, GameUI.Gold, true);
            bool zoom = false;
            ui.Button(c, "Toggle detail zoom", 680, 354, 365, 48, () => { zoom = !zoom; detail.fontSize = zoom ? 25 : 21; });
            int total = mission.evidence.Count(e => e.suspicious);
            var verdict = ui.Button(c, "HIGHLIGHT " + total + " INDICATORS FIRST", 0, 432, 650, 52, ShowQuestion, GameUI.Cyan);
            verdict.interactable = false;
            for (int i = 0; i < mission.evidence.Length; i++)
            {
                int index = i; var item = mission.evidence[i];
                var button = ui.Button(document, item.label + "   " + item.text, 14, 178 + i * 53, 622, 46, () => {
                    detail.text = game.Personalize(item.explanation);
                    detail.color = item.suspicious ? GameUI.Gold : GameUI.Teal;
                    if (item.suspicious)
                    {
                        marked.Add(index);
                        progress.text = marked.Count + " / " + total + " indicators highlighted";
                        if (marked.Count == total)
                        {
                            verdict.interactable = true; verdict.GetComponentInChildren<Text>().text = "REPORT EVIDENCE / COMPLETE   >";
                        }
                    }
                });
                button.onClick.AddListener(() => { if (item.suspicious) button.GetComponent<Image>().color = GameUI.Hex("665237"); });
            }
        }

        void ShowTerminal(int selected = 0)
        {
            var state = game.Progress.terminal[selected];
            var terminal = new VirtualTerminal(state.id, state);
            var challenge = terminal.Challenge;
            int solved = game.Progress.terminal.Count(t => t.solved);
            var c = ui.OpenModal("LINUX CTF / SYSADMIN TUX", mission.title, "FLAGS " + solved + "/" + TerminalChallenges.All.Length + "  /  Three isolated, fictional filesystems. Hints cost 5 XP each.");
            ui.Box(c, "Terminal", 0, 0, 740, 416, GameUI.Ink);
            var log = ui.Scroll(c, "Terminal history", 14, 12, 710, 316, 316);
            log.GetComponentInParent<ScrollRect>().GetComponent<Image>().color = GameUI.Ink;
            var transcript = ui.Text(log, "", 0, 0, 700, 316, 17, GameUI.Teal);
            var command = ui.Input(c, 14, 351, 566, 49, "trainee@cybercity:~$  command");
            command.gameObject.name = "Terminal command";
            var history = new List<string> { "CyberCity Training Linux / " + challenge.title, "Type help to inspect available commands.", "" };
            for (int i = 0; i < state.hints; i++) history.Add("HINT: " + challenge.hints[i]);
            System.Action refresh = () => {
                while (history.Count > 120) history.RemoveAt(0);
                transcript.text = game.Personalize(string.Join("\n", history));
                float height = Mathf.Max(316, history.Count * 23f);
                transcript.rectTransform.sizeDelta = new Vector2(700, height); log.sizeDelta = new Vector2(710, height);
                Canvas.ForceUpdateCanvases(); log.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0;
            };
            System.Action run = () => {
                string text = command.text.Trim(); if (text.Length == 0) return;
                if (text == "clear") history.Clear();
                else { history.Add("> " + text); history.AddRange(terminal.Execute(text).Split('\n')); }
                command.text = ""; refresh();
                if (terminal.Solved) { game.Save(); ShowTerminal(selected); }
                else { if (text.StartsWith("submit ")) game.WrongAnswer(); command.ActivateInputField(); }
            };
            ui.Button(c, "RUN", 594, 351, 128, 49, run, GameUI.Cyan);
            command.onEndEdit.AddListener(_ => { if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) run(); });
            for (int i = 0; i < TerminalChallenges.All.Length; i++)
            {
                int index = i; var item = TerminalChallenges.All[i];
                ui.Button(c, (game.Progress.terminal[i].solved ? "OK  " : "0" + (i + 1) + "  ") + item.title, 770, i * 56, 280, 46, () => ShowTerminal(index), i == selected ? GameUI.Cyan : (Color?)null);
            }
            ui.Text(c, challenge.objective, 770, 184, 280, 124, 19, GameUI.White);
            Button hintButton = null;
            hintButton = ui.Button(c, "HINT " + (Mathf.Min(state.hints + 1, 2)) + "/2  (-5 XP)", 770, 351, 280, 49, () => {
                if (state.hints < challenge.hints.Length && !state.solved) { state.hints++; game.Progress.hintUses[game.Progress.completed]++; game.Save(); }
                history.Add("HINT: " + challenge.hints[Mathf.Max(0, state.hints - 1)]); refresh();
                hintButton.GetComponentInChildren<Text>().text = "HINT " + Mathf.Min(state.hints + 1, 2) + "/2  (-5 XP)";
                hintButton.interactable = state.hints < 2 && !state.solved;
            });
            hintButton.interactable = state.hints < 2 && !state.solved;
            if (solved == TerminalChallenges.All.Length)
                ui.Button(c, "ALL FLAGS VERIFIED / COMPLETE MISSION", 0, 435, 740, 49, Finish, GameUI.Cyan);
            else ui.Text(c, state.solved ? "Flag verified. Choose another challenge from the board." : "Read evidence, investigate and submit <flag>. All three flags are required.", 0, 435, 1040, 48, 18, GameUI.Muted);
            refresh(); command.ActivateInputField();
        }

        void ShowWebLab()
        {
            new FakeBrowser(ui, mission, mission.web, game.WebState(mission, mission.kind), Finish).Show();
        }

        void ShowFinal()
        {
            foreach (var lab in mission.finalLabs)
            {
                var state = game.WebState(mission, lab.kind);
                if (!state.verified) { new FakeBrowser(ui, mission, lab, state, ShowFinal).Show(); return; }
            }
            new SocUI(ui, mission, game.SocState(mission), Finish).Show();
        }
    }
}
