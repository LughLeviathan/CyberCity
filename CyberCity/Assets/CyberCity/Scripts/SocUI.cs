using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CyberCity
{
    public sealed class SocUI
    {
        readonly GameUI ui;
        readonly Mission mission;
        readonly SocSession session;
        readonly Action completed;
        readonly HashSet<string> responses;
        string selectedAlert, message = "Inspect alerts and their packet references. A burst of traffic can be authorized maintenance.";
        RectTransform rows, work, suggestions;
        InputField filter;
        Text detail, feedback;
        public SocUI(GameUI ui, Mission mission, SocProgress state, Action completed)
        {
            this.ui = ui; this.mission = mission; session = new SocSession(mission.soc, state); this.completed = completed;
            responses = new HashSet<string>(state.responses);
        }
        public void Show()
        {
            if (session.State.complete) { ShowReport(); return; }
            var c = ui.OpenModal("BLUE SOC / FICTIONAL SIEM", mission.soc.title, "1 Triage  >  2 Find source  >  3 Tag attacks  >  4 Respond  >  5 Report    /    Stage " + (session.Stage + 1));
            ui.Text(c, "ALERT QUEUE", 0, 0, 208, 25, 13, GameUI.Cyan, true);
            ui.Text(c, "TRAFFIC / " + mission.soc.packets.Length + " FICTIONAL RECORDS", 224, 0, 546, 25, 13, GameUI.Cyan, true);
            ui.Text(c, "EVIDENCE DETAILS", 790, 0, 266, 25, 13, GameUI.Cyan, true);
            var alertQueue = ui.Scroll(c, "Alert queue", 0, 34, 208, 352, mission.soc.alerts.Length * 110);
            for (int i = 0; i < mission.soc.alerts.Length; i++)
            {
                var alert = mission.soc.alerts[i]; bool done = session.State.triaged.Contains(alert.id);
                var b = ui.Button(alertQueue, (done ? "OK / " : "OPEN / ") + alert.title, 0, i * 110, 208, 96, () => InspectAlert(alert), done ? GameUI.Teal : (Color?)null);
                b.gameObject.name = "Alert-" + alert.id; b.GetComponentInChildren<Text>().fontSize = 16;
            }
            ui.Button(c, "TRUE POSITIVE", 0, 400, 208, 40, () => Triage(true)).GetComponentInChildren<Text>().fontSize = 14;
            ui.Button(c, "FALSE POSITIVE", 0, 448, 208, 40, () => Triage(false)).GetComponentInChildren<Text>().fontSize = 14;
            filter = ui.Input(c, 224, 34, 370, 42, "http, dns, free text, or ip.src == ..."); filter.text = session.State.filter; filter.gameObject.name = "SOC filter";
            ui.Button(c, "APPLY FILTER", 604, 34, 166, 42, () => ApplyFilter()).GetComponentInChildren<Text>().fontSize = 14;
            ui.Button(c, "HTTP", 224, 84, 100, 34, () => { filter.text = "http"; ApplyFilter(); });
            ui.Button(c, "DNS", 334, 84, 100, 34, () => { filter.text = "dns"; ApplyFilter(); });
            ui.Button(c, "Source IP...", 444, 84, 190, 34, () => {
                filter.text = "ip.src == "; suggestions.gameObject.SetActive(true);
            });
            ui.Button(c, "ALL", 644, 84, 126, 34, () => { filter.text = ""; ApplyFilter(); });
            ui.Text(c, "TIME        SOURCE             DESTINATION        PROTO   INFO", 224, 127, 546, 20, 12, GameUI.Muted, true);
            rows = ui.Scroll(c, "Traffic table", 224, 151, 546, 222, mission.soc.packets.Length * 54);
            var detailContent = ui.Scroll(c, "Packet details", 790, 34, 266, 214, 480);
            detail = ui.Text(detailContent, "Select an alert or packet to inspect its context.", 8, 8, 246, 460, 17, GameUI.White);
            feedback = ui.Text(c, message, 790, 264, 266, 168, 17, GameUI.Gold);
            ui.Button(c, "INCIDENT REPORT", 790, 444, 266, 44, () => {
                if (session.Stage < 4) { feedback.text = "Complete the investigation steps before filing your report."; return; }
                ShowReport();
            }).GetComponentInChildren<Text>().fontSize = 14;
            work = ui.Rect(c, "Investigation task", 224, 386, 546, 106);
            suggestions = ui.Box(c, "Source IP suggestions", 224, 122, 546, 184, GameUI.Panel);
            var sources = mission.soc.packets.Select(p => p.src).Distinct().ToArray();
            for (int i = 0; i < sources.Length; i++)
            {
                string ip = sources[i]; ui.Button(suggestions, ip, 8, 8 + i * 42, 530, 36, () => { filter.text = "ip.src == " + ip; suggestions.gameObject.SetActive(false); ApplyFilter(); });
            }
            suggestions.gameObject.SetActive(false);
            DrawRows(); DrawTask();
            var packet = mission.soc.packets.FirstOrDefault(p => p.id == session.State.selectedPacket);
            if (packet != null) InspectPacket(packet);
        }
        void InspectAlert(SocAlert alert)
        {
            selectedAlert = alert.id;
            detail.text = ui.Game.Personalize(alert.title + "\n\n" + alert.context + "\n\nPacket references: " + string.Join(", ", alert.packetIds));
            feedback.text = "Use packet context to mark this alert true or false positive.";
        }
        void Triage(bool truePositive)
        {
            if (selectedAlert == null) { feedback.text = "Inspect an alert before recording a verdict."; return; }
            bool accepted = session.Triage(selectedAlert, truePositive, out message); ui.Game.Save();
            if (!accepted) { ui.Game.WrongAnswer(); feedback.text = ui.Game.Personalize(message); return; }
            Show();
        }
        void ApplyFilter()
        {
            bool valid = session.ApplyFilter(filter.text); feedback.text = valid ? session.Visible.Length + " matching records. Select a row to inspect it." : session.FilterError;
            if (string.IsNullOrEmpty(session.State.selectedPacket)) detail.text = "Select a packet from a valid filter result to inspect evidence.";
            ui.Game.Save(); DrawRows();
        }
        void DrawRows()
        {
            foreach (Transform child in rows) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            rows.sizeDelta = new Vector2(546, Mathf.Max(222, session.Visible.Length * 54));
            for (int i = 0; i < session.Visible.Length; i++)
            {
                var packet = session.Visible[i];
                var b = ui.Button(rows, packet.time, 0, i * 54, 542, 48, () => { session.SelectPacket(packet.id); InspectPacket(packet); ui.Game.Save(); });
                b.gameObject.name = "Packet-" + packet.id;
                var time = b.GetComponentInChildren<Text>(); time.fontSize = 13; time.rectTransform.sizeDelta = new Vector2(47, 48); time.rectTransform.anchoredPosition = new Vector2(5, 0);
                ui.Text(b.transform, packet.src, 56, 8, 112, 32, 13, GameUI.White);
                ui.Text(b.transform, packet.dst, 174, 8, 110, 32, 13, GameUI.Muted);
                ui.Text(b.transform, packet.protocol, 290, 8, 44, 32, 12, GameUI.Cyan);
                ui.Text(b.transform, packet.info, 342, 6, 192, 36, 13, GameUI.White);
            }
        }
        void InspectPacket(PacketLog packet)
        {
            string tags = string.Join(", ", mission.soc.attacks.Where(a => a.packetIds.Contains(packet.id) && session.State.classified.Contains(a.id)).Select(a => a.kind));
            detail.text = ui.Game.Personalize(packet.id + " / " + packet.time + "\n" + packet.src + " -> " + packet.dst + "\n" + packet.protocol + "\n\n" + packet.info + (tags.Length > 0 ? "\n\nTAGGED: " + tags : ""));
        }
        void DrawTask()
        {
            foreach (Transform child in work) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            if (session.Stage == 0) ui.Text(work, "Inspect and triage every alert.\n" + session.State.triaged.Length + "/" + mission.soc.alerts.Length + " verdicts recorded.", 0, 4, 546, 92, 19, GameUI.White);
            else if (session.Stage == 1)
            {
                var source = ui.Input(work, 0, 0, 340, 44, "Attacker source IP from evidence"); source.gameObject.name = "SOC attacker";
                ui.Button(work, "SET ATTACKER", 350, 0, 196, 44, () => {
                    bool valid = session.IdentifyAttacker(source.text, out message); CheckTask(valid);
                }).GetComponentInChildren<Text>().fontSize = 14;
                ui.Text(work, "Apply a filter and select a supporting attack row first.", 0, 56, 546, 48, 17, GameUI.Muted);
            }
            else if (session.Stage == 2)
            {
                string[] kinds = { "IDOR", "XSS", "SQLi" };
                for (int i = 0; i < kinds.Length; i++)
                { string kind = kinds[i]; ui.Button(work, "TAG " + kind, i * 184, 0, 178, 44, () => { bool valid = session.Classify(kind, out message); CheckTask(valid); }); }
                ui.Text(work, "Select evidence, then tag its attack. " + session.State.classified.Length + "/" + mission.soc.attacks.Length + " classified.", 0, 56, 546, 44, 17, GameUI.Muted);
            }
            else if (session.Stage == 3)
            {
                string[] ids = { "block-ip", "patch", "reset-credentials" }, labels = { "BLOCK IP", "PATCH APPS", "RESET ACCOUNTS" };
                for (int i = 0; i < ids.Length; i++)
                {
                    string id = ids[i]; string label = labels[i];
                    ui.Button(work, (responses.Contains(id) ? "[ON] " : "[ ] ") + label, i * 184, 0, 178, 40, () => {
                        if (!responses.Add(id)) responses.Remove(id); session.State.responses = responses.ToArray(); ui.Game.Save(); DrawTask();
                    }).GetComponentInChildren<Text>().fontSize = 12;
                }
                ui.Button(work, "APPLY RESPONSE", 0, 50, 546, 44, () => { bool valid = session.Respond(responses.ToArray(), out message); CheckTask(valid); }, GameUI.Cyan);
            }
            else
            {
                ui.Text(work, "Firewall, application patches and account reset simulated.\nEvidence retained. Complete the incident report.", 0, 0, 546, 60, 17, GameUI.Teal);
                ui.Button(work, "OPEN REPORT TEMPLATE", 0, 66, 546, 40, ShowReport, GameUI.Cyan);
            }
        }
        void CheckTask(bool valid)
        {
            if (!valid) ui.Game.WrongAnswer(); else ui.Game.Save();
            feedback.text = ui.Game.Personalize(message); DrawTask();
        }
        void ShowReport()
        {
            var c = ui.OpenModal("SOC / INCIDENT REPORT", "Write the case record, {player}.", "Use the evidence you inspected. Your source, attack tags, time, impact and mitigation must agree.");
            string[] labels = { "WHO / source IP", "WHAT / attack types", "WHEN / first seen", "IMPACT / affected data", "MITIGATION / actions" };
            string[] names = { "who", "what", "when", "impact", "mitigation" };
            var report = session.State.report;
            string[] values = { report.who, report.what, report.when, report.impact, report.mitigation };
            var inputs = new InputField[5];
            for (int i = 0; i < labels.Length; i++)
            {
                ui.Text(c, labels[i], 0, i * 70 + 10, 210, 44, 17, GameUI.Cyan, true);
                inputs[i] = ui.Input(c, 224, i * 70, 832, 52, labels[i]); inputs[i].gameObject.name = "Report-" + names[i]; inputs[i].characterLimit = 240; inputs[i].text = values[i];
                inputs[i].onValueChanged.AddListener(_ => { StoreReport(inputs, report); });
                inputs[i].onEndEdit.AddListener(_ => ui.Game.Save());
            }
            var result = ui.Text(c, session.State.complete ? "Report accepted. All evidence agrees." : "Explain the impact and include the block, patch and reset actions.", 0, 358, 1040, 60, 18, GameUI.Muted);
            ui.Button(c, session.State.complete ? "COMPLETE SOC MISSION" : "SUBMIT INCIDENT REPORT", 0, 432, 620, 56, () => {
                StoreReport(inputs, report);
                if (!session.State.complete && !session.SubmitReport(report, out message)) { ui.Game.WrongAnswer(); result.text = ui.Game.Personalize(message); result.color = GameUI.Gold; return; }
                ui.Game.Save(); completed();
            }, GameUI.Cyan);
            ui.Button(c, "BACK TO EVIDENCE", 640, 432, 416, 56, () => { StoreReport(inputs, report); ui.Game.Save(); Show(); });
        }
        static void StoreReport(InputField[] inputs, IncidentReport report)
        {
            if (inputs.Any(i => i == null)) return;
            report.who = inputs[0].text; report.what = inputs[1].text; report.when = inputs[2].text; report.impact = inputs[3].text; report.mitigation = inputs[4].text;
        }
    }
}
