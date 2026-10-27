#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace CyberCity.Editor
{
    public static class CyberCityVerification
    {
        static readonly List<string> checks = new List<string>();
        static readonly Queue<Action> actions = new Queue<Action>();
        static readonly Dictionary<string, byte[]> saveBackup = new Dictionary<string, byte[]>();
        static double next;
        static int nextFrame;
        static InputSettings originalInputSettings, verificationInputSettings;
        static Keyboard verificationKeyboard, originalKeyboard;
        static readonly List<Keyboard> pausedKeyboards = new List<Keyboard>();
        static bool running;
        static CyberCityGame game;
        static Vector3 movementStart;
        static string failure;
        public static string Status { get; private set; } = "Not run";
        static void Check(bool condition, string name)
        {
            if (!condition) { Status = "FAILED: " + name; WriteReport(); throw new Exception("FAIL: " + name); }
            checks.Add(name);
        }

        [MenuItem("Tools/CyberCity/Verify core systems")]
        public static void VerifyCore()
        {
            checks.Clear(); failure = null;
            var campaign = CampaignLoader.Parse(Resources.Load<TextAsset>("campaign").text);
            var shortPack = new Campaign { version = 1, title = "Short training pack", missions = campaign.missions.Take(2).ToArray() };
            Check(CampaignLoader.Parse(JsonUtility.ToJson(shortPack)).missions.Length == 2, "Mission count follows scenario pack");
            Check(campaign.missions.Length > 0, "Validated offline missions");
            Check(campaign.missions.Select(m => m.id).Distinct().Count() == campaign.missions.Length, "Unique mission IDs");
            Check(CampaignLoader.Hash(Resources.Load<TextAsset>("campaign").text) == Resources.Load<TextAsset>("campaign_hash").text.Trim(), "Bundled campaign SHA-256 matches");
            bool rejected = false; try { CampaignLoader.Parse("{\"version\":2}"); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Invalid campaign rejected");
            var malformed = JsonUtility.FromJson<Campaign>(Resources.Load<TextAsset>("campaign").text);
            malformed.missions[0].questions = null; rejected = false;
            try { CampaignLoader.Parse(JsonUtility.ToJson(malformed)); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Missing learning questions rejected");
            malformed = JsonUtility.FromJson<Campaign>(Resources.Load<TextAsset>("campaign").text);
            malformed.missions[0].npc = "Unknown mentor"; rejected = false;
            try { CampaignLoader.Parse(JsonUtility.ToJson(malformed)); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Unsupported scenario mentor rejected");
            Check(PlayerIdentity.TryValidate("  Byte_42  ", out string cleanName, out _) && cleanName == "Byte_42", "Username trims surrounding whitespace");
            foreach (string valid in new[] { "ABC", "A B", "Byte_42", "abcdefghijklmnop", "Nadia42" })
                Check(PlayerIdentity.TryValidate(valid, out _, out _), "Valid username: " + valid);
            foreach (string invalid in new[] { "", "   ", "ab", "abcdefghijklmnopq", "<b>x</b>", "alice!", "A\nB", "A\tB" })
                Check(!PlayerIdentity.TryValidate(invalid, out _, out string reason) && reason.Length > 0, "Invalid username rejected with inline reason");
            Check(PlayerIdentity.Personalize("Welcome, {player}. Thanks, {player}.", "Byte_42") == "Welcome, Byte_42. Thanks, Byte_42.", "Every player token resolves as plain text");
            Check(!PlayerIdentity.Personalize("Welcome, {player}.", "<invalid>").Contains("{player}"), "Invalid or missing identity uses a safe fallback");
            malformed = JsonUtility.FromJson<Campaign>(Resources.Load<TextAsset>("campaign").text);
            malformed.missions[1].id = malformed.missions[0].id; rejected = false;
            try { CampaignLoader.Parse(JsonUtility.ToJson(malformed)); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Duplicate mission IDs rejected");
            malformed = JsonUtility.FromJson<Campaign>(Resources.Load<TextAsset>("campaign").text);
            foreach (var m in malformed.missions.Where(m => m.kind != "quiz")) m.questions = null;
            Check(CampaignLoader.Parse(JsonUtility.ToJson(malformed)) != null, "Non-quiz learning does not require questions");
            var terminal = new VirtualTerminal();
            Check(!terminal.Execute("submit " + VirtualTerminal.Flag).Contains("VERIFIED"), "Flag requires reading evidence first");
            Check(terminal.Execute("ls").Contains("README.txt") && !terminal.Execute("ls").Contains(".archive"), "Normal listing hides dot entries");
            Check(terminal.Execute("ls -a").Contains(".archive/"), "Hidden folder appears with ls -a");
            terminal.Execute("cd .archive");
            Check(terminal.Execute("pwd") == "/home/citizen/.archive", "Terminal navigation");
            Check(terminal.Execute("cat access.txt").Contains(VirtualTerminal.Flag), "Terminal reads evidence");
            Check(!terminal.Execute("submit wrong").Contains("VERIFIED"), "Incorrect flag rejected");
            terminal.Execute("submit " + VirtualTerminal.Flag); Check(terminal.Solved, "Correct flag accepted");
            terminal.Execute("cd ../../../../../../"); Check(terminal.Directory == "/", "Virtual path stays inside root");
            Check(terminal.Execute("curl https://example.com").StartsWith("Unknown command"), "External commands are unsupported");
            VerifyTerminalChallenges();
            VerifyWebLabs(campaign);
            foreach (var mission in campaign.missions.Where(m => m.kind == "soc")) VerifySoc(mission.soc);
            VerifyFinalRequirements(campaign);
            VerifyLocalPacks(campaign);
            VerifySaveEvidence(campaign);
            VerifyCapturedRequestSave(campaign);
            VerifySocFilterEvidence(campaign);
            string folder = Path.Combine(Application.temporaryCachePath, "CyberCityVerification"); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "progress.json");
            foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
            var first = ProgressStore.Create(campaign); first.completed = 3; first.score = 450; first.x = 5; first.y = -12; first.started = true; first.playerName = "TestPatrol";
            Check(ProgressStore.Save(first, path), "Atomic save succeeds");
            var loaded = ProgressStore.Load(campaign, path);
            Check(loaded.completed == 3 && loaded.score == 450 && loaded.x == 5 && loaded.playerName == "TestPatrol", "Save round trip preserves identity, progress and position");
            first.completed = 4; first.score = 600; ProgressStore.Save(first, path);
            File.WriteAllText(path, "damaged");
            Check(ProgressStore.Load(campaign, path).completed == 3, "Damaged primary save recovers backup");
            File.WriteAllText(path + ".bak", "damaged");
            Check(ProgressStore.Load(campaign, path).completed == 0, "Damaged primary and backup recover fresh data");
            File.WriteAllText(path, "{\"version\":1,\"completed\":10,\"score\":1500}");
            Check(ProgressStore.Load(campaign, path).version == 2 && ProgressStore.LastError.Contains("version 1"), "Legacy save resets safely with an explanation");
            var compact = ProgressStore.Create(shortPack); compact.completed = 2; compact.score = 300;
            Check(compact.attempts.Length == 2 && ProgressStore.Save(compact, path), "Retry storage follows mission count");
            Check(ProgressStore.Load(shortPack, path).score == 300, "Dynamic maximum score accepted");
            compact.score = 301; ProgressStore.Save(compact, path);
            Check(ProgressStore.Load(shortPack, path).score == 300, "Over-maximum score recovers valid backup");
            Check(ProgressStore.Load(campaign, path).completed == 0, "Different course route resets safely");
            var resumed = ProgressStore.Create(campaign); resumed.playerName = "Byte_42";
            resumed.terminal[0].solved = true; resumed.terminal[1].hints = 2; resumed.hintUses[0] = 2;
            ProgressStore.Save(resumed, path); var restored = ProgressStore.Load(campaign, path);
            Check(restored.terminal[0].solved && restored.terminal[1].hints == 2 && restored.hintUses[0] == 2, "Flags and paid hints survive save/reload");
            foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
            Status = "PASS: " + checks.Count + " core checks"; WriteReport(); Debug.Log("CyberCity " + Status);
        }

        static void VerifyTerminalChallenges()
        {
            var logs = new VirtualTerminal("log-hunt");
            Check(!logs.Solved && !logs.Execute("submit CYBER{203.0.113.42}").Contains("VERIFIED"), "Log flag needs evidence before submission");
            Check(logs.Execute("find . -name auth.log").Contains("/logs/auth.log"), "find locates the authentication evidence");
            Check(logs.Execute("cat logs/auth.log | grep Failed | wc -l") == "4", "Pipe isolates four failed sign-ins from noise");
            Check(logs.Execute("grep 203.0.113.42 logs/auth.log | wc -l") == "3", "Repeated attacker counted from evidence");
            Check(logs.Execute("head -n 1 logs/auth.log").StartsWith("09:00 Failed") && logs.Execute("tail -n 1 logs/auth.log").StartsWith("09:04 Failed"), "head and tail inspect boundaries");
            Check(logs.Execute("head -n nope logs/auth.log").StartsWith("ERROR:") && logs.Execute("cat missing").StartsWith("ERROR:"), "Bad command arguments return useful errors");
            logs.Execute("submit CYBER{203.0.113.77}"); Check(!logs.Solved, "Wrong log source rejected");
            logs.Execute("submit CYBER{203.0.113.42}"); Check(logs.Solved, "Log hunt solved by highest failed source");
            var permissions = new VirtualTerminal("permissions");
            Check(permissions.Execute("whoami") == "trainee" && permissions.Execute("ls -l private").Contains("---------- trainee"), "Permissions show current owner and missing read access");
            Check(permissions.Execute("cat private/flag.txt").Contains("Permission denied"), "Unreadable evidence stays protected");
            permissions.Execute("chmod 777 private/flag.txt"); permissions.Execute("cat private/flag.txt");
            Check(permissions.Execute("submit CYBER{owner_read_only}").Contains("Least privilege") && !permissions.Solved, "Excessive permissions cannot solve challenge");
            permissions.Execute("chmod 400 private/flag.txt"); permissions.Execute("cat private/flag.txt");
            permissions.Execute("submit wrong"); Check(!permissions.Solved, "Wrong permissions flag rejected");
            permissions.Execute("submit CYBER{owner_read_only}"); Check(permissions.Solved, "Owner-only read permission solves challenge");
            var symbolic = new VirtualTerminal("permissions"); symbolic.Execute("chmod u+r private/flag.txt");
            Check(symbolic.Execute("cat private/flag.txt").Contains("CYBER{owner_read_only}"), "Symbolic owner read command supported");
            Check(!new VirtualTerminal("hidden-file").Execute("cat logs/auth.log").Contains("Failed"), "Challenge filesystems are isolated");
            Check(ProgressStore.Reward(0, 2) == 140 && ProgressStore.Reward(100, 6) == 75, "Hints reduce reward without crossing minimum");
        }

        static void VerifyWebLabs(Campaign campaign)
        {
            foreach (var mission in campaign.missions.Where(m => m.kind == "idor" || m.kind == "xss" || m.kind == "sqli"))
            {
                var s = mission.web;
                string target = s.kind == "idor" ? "https://" + s.host + s.path + "?id=" + s.targetId : s.StartUrl;
                string[] payloads = s.kind == "idor" ? new[] { "" } : s.kind == "xss" ? new[] { "<script>alert(1)</script>", "<IMG SRC=x ONERROR=alert(2)>", "<svg onload=alert(3)>" } :
                    new[] { "' OR '1'='1", "' OR 1=1 --", "admin'--", "' oR true --" };
                var empty = new WebLabSession(s, new WebProgress());
                Check(!empty.ApplyPatch(), "Cannot patch before observing flaw: " + s.kind);
                Check(empty.Request("https://outside.example/").status == 400 && empty.Request("/missing").status == 404, "Router keeps requests within fictional scenario: " + s.kind);
                empty.Request(s.StartUrl, s.kind == "sqli" ? "' OR 1=2" : "ordinary training text");
                Check(!empty.State.demonstrated, "Benign or false condition cannot demonstrate attack: " + s.kind);
                foreach (string payload in payloads)
                {
                    var state = new WebProgress { missionId = mission.id, kind = s.kind };
                    var lab = new WebLabSession(s, state); var before = lab.Request(target, payload);
                    Check(state.demonstrated && !state.verified && before.status == 200, "Varied attack demonstrated: " + s.kind + " / " + payload);
                    Check(s.kind != "xss" || before.fakeAlert, "XSS produces a fake game alert only");
                    Check(lab.ApplyPatch() && !state.verified, "Applying source fix still requires retest: " + s.kind);
                    lab.Request(target, payload, "different-password");
                    Check(!state.verified, "Changed form body does not verify captured request: " + s.kind);
                    lab.Request(s.StartUrl, "different request");
                    Check(!state.verified, "Different request does not verify captured attack: " + s.kind);
                    var reloaded = new WebLabSession(s, JsonUtility.FromJson<WebProgress>(JsonUtility.ToJson(state)));
                    var after = reloaded.Request(target, payload);
                    Check(reloaded.State.verified && !after.fakeAlert && (s.kind == "idor" ? after.status == 403 : s.kind == "sqli" ? after.status == 401 : after.page.Contains("&lt;")), "Same attack is rejected or encoded after patch and reload: " + s.kind);
                    Check(s.kind != "sqli" || after.network.Contains("@name"), "SQLi patch uses a bound value in displayed query");
                }
            }
        }

        static void VerifySoc(SocScenario scenario)
        {
            var state = new SocProgress(); var soc = new SocSession(scenario, state);
            Check(!soc.SubmitReport(new IncidentReport(), out _) && !state.complete, "SOC report cannot skip investigation stages");
            var first = scenario.alerts[0];
            Check(!soc.Triage(first.id, !first.truePositive, out _) && state.triaged.Length == 0, "Wrong triage verdict cannot advance");
            foreach (var alert in scenario.alerts) Check(soc.Triage(alert.id, alert.truePositive, out _), "Alert triaged from context: " + alert.id);
            Check(soc.Stage == 1, "All triage verdicts precede source identification");
            Check(!soc.ApplyFilter("ip.src = 203.0.113.42") && soc.FilterError.Contains("syntax"), "Malformed filter explains expected syntax");
            Check(!soc.ApplyFilter("ip.src == 203.0.113.999"), "Invalid filter IP rejected");
            Check(soc.ApplyFilter("dns") && soc.Visible.All(p => p.protocol == "DNS"), "DNS quick filter excludes web attacks");
            Check(!soc.IdentifyAttacker(scenario.attackerIp, out _), "Guessed source needs filtered packet evidence");
            Check(soc.ApplyFilter("<script>") && soc.Visible.Length > 0 && soc.Visible.All(p => p.info.Contains("<script>")), "Free-text filter matches packet body");
            soc.ApplyFilter("ip.src == " + scenario.attackerIp);
            Check(soc.Visible.All(p => p.src == scenario.attackerIp) && soc.Visible.Length > 0, "Source filter correlates attacker traffic");
            string firstPacket = scenario.attacks[0].packetIds[0]; soc.SelectPacket(firstPacket);
            Check(!soc.IdentifyAttacker("203.0.113.7", out _), "Maintenance source cannot impersonate attacker");
            Check(soc.IdentifyAttacker(scenario.attackerIp, out _) && soc.Stage == 2, "Attacker confirmed from actual filtered evidence");
            Check(!soc.Respond(scenario.requiredResponses, out _), "Response cannot skip attack classification");
            foreach (var attack in scenario.attacks)
            {
                soc.SelectPacket(attack.packetIds[0]);
                Check(!soc.Classify(attack.kind == "IDOR" ? "XSS" : "IDOR", out _), "Wrong attack tag rejected: " + attack.id);
                Check(soc.Classify(attack.kind, out _), "Evidence classified: " + attack.kind);
            }
            Check(!soc.Respond(new[] { "block-ip" }, out _), "Containment alone leaves root cause and accounts exposed");
            Check(soc.Respond(scenario.requiredResponses, out _) && soc.Stage == 4, "Simulated response requires all correct actions");
            Check(!soc.SubmitReport(new IncidentReport { who = scenario.attackerIp }, out _), "Incomplete incident report rejected");
            var report = new IncidentReport { who = scenario.attackerIp, what = string.Join(", ", scenario.attacks.Select(a => a.kind)), when = scenario.firstSeen,
                impact = "Private profiles and accounts were exposed.", mitigation = "Blocked the source, patched the apps and reset credentials." };
            Check(soc.SubmitReport(report, out _) && state.complete, "Evidence-aligned incident report closes the case");
            var resumed = new SocSession(scenario, JsonUtility.FromJson<SocProgress>(JsonUtility.ToJson(state)));
            Check(resumed.Stage == 5 && resumed.State.report.who == scenario.attackerIp, "SOC report and investigation stage survive reload");
        }

        static void VerifyFinalRequirements(Campaign campaign)
        {
            string[] route = { "orientation", "awareness", "phishing", "fake-site", "linux-ctf", "idor", "xss", "sqli", "blue-soc", "final", "graduation" };
            Check(campaign.missions.Select(m => m.id).SequenceEqual(route), "Bundled course follows the revised route with no redundant team quizzes");
            Check(campaign.missions.Sum(m => m.questions?.Length ?? 0) == 6 && campaign.missions.Where(m => m.id != "orientation" && m.id != "awareness" && m.id != "graduation").All(m => (m.questions?.Length ?? 0) == 0), "Six light questions are limited to orientation, awareness and graduation");
            Check(CampaignLoader.UnlockForNpc(campaign, "Red") == 5 && CampaignLoader.UnlockForNpc(campaign, "Blue") == 8, "Red follows Linux CTF and Blue follows SQLi");
            var progress = ProgressStore.Create(campaign);
            var linux = campaign.missions.First(m => m.id == "linux-ctf");
            progress.terminal[0].solved = true; progress.terminal[1].solved = true;
            Check(!MissionRequirements.IsSatisfied(linux, progress), "Two flags cannot complete Linux CTF");
            progress.terminal[2].solved = true; Check(MissionRequirements.IsSatisfied(linux, progress), "Three flags satisfy Linux CTF");
            var final = campaign.missions.First(m => m.kind == "final");
            Check(!MissionRequirements.IsSatisfied(final, progress), "Final chain begins without completion evidence");
            progress.web = final.finalLabs.Select(l => new WebProgress { missionId = final.id, kind = l.kind, verified = true }).ToArray();
            Check(!MissionRequirements.IsSatisfied(final, progress), "Both browser fixes still require final incident analysis");
            progress.soc = new[] { new SocProgress { missionId = final.id, complete = true } };
            Check(MissionRequirements.IsSatisfied(final, progress), "Final requires browser chain and incident analysis together");
            foreach (var lab in final.finalLabs)
            {
                var state = new WebProgress(); var session = new WebLabSession(lab, state);
                string url = lab.kind == "idor" ? "https://" + lab.host + lab.path + "?id=" + lab.targetId : lab.StartUrl;
                string payload = lab.kind == "idor" ? "" : "admin'--";
                session.Request(url, payload); session.ApplyPatch(); session.Request(url, payload);
                Check(state.verified, "Final browser stage supports attack, patch and retest: " + lab.kind);
            }
        }

        static void VerifyLocalPacks(Campaign bundled)
        {
            string folder = Path.Combine(Application.persistentDataPath, "ScenarioPacks");
            string path = Path.Combine(folder, "campaign.json"), checksum = path + ".sha256";
            var backup = new Dictionary<string, byte[]>();
            foreach (string file in new[] { path, checksum }) backup[file] = File.Exists(file) ? File.ReadAllBytes(file) : null;
            try
            {
                Directory.CreateDirectory(folder);
                var pack = new Campaign { version = 1, title = "Verified compact course", missions = bundled.missions.Take(2).ToArray() };
                string json = JsonUtility.ToJson(pack); File.WriteAllText(path, json); File.WriteAllText(checksum, CampaignLoader.Hash(json));
                Check(CampaignLoader.Load().missions.Length == 2 && CampaignLoader.Status == "LOCAL PACK / VERIFIED", "Verified local pack can use a different mission count");
                File.WriteAllText(checksum, "invalid");
                Check(CampaignLoader.Load().missions.Length == bundled.missions.Length && CampaignLoader.Status == "OFFLINE PACK / VERIFIED", "Corrupt optional pack falls back to verified offline course");
                File.WriteAllText(path, "{\"version\":1,\"missions\":[]}"); File.WriteAllText(checksum, CampaignLoader.Hash(File.ReadAllText(path)));
                Check(CampaignLoader.Load().missions.Length == bundled.missions.Length, "Malformed but hashed pack falls back safely");
            }
            finally
            {
                foreach (var pair in backup)
                    if (pair.Value != null) File.WriteAllBytes(pair.Key, pair.Value); else if (File.Exists(pair.Key)) File.Delete(pair.Key);
                CampaignLoader.Load();
            }
        }

        public static void VerifySaveEvidence(Campaign campaign)
        {
            string path = ReviewSavePath();
            try
            {
                var valid = ProgressStore.Create(campaign); valid.completed = 4; valid.score = 600; ProgressStore.Save(valid, path);
                valid.completed = 6; valid.score = 900; valid.terminal = null; valid.web = null; valid.soc = null;
                ProgressStore.Save(valid, path);
                Check(ProgressStore.Load(campaign, path).completed == 4, "Completed practical missions require saved evidence or recover backup");
            }
            finally { DeleteReviewSave(path); }
        }
        public static void VerifyCapturedRequestSave(Campaign campaign)
        {
            foreach (bool invalidUrl in new[] { true, false })
            {
                string path = ReviewSavePath();
                try
                {
                    var valid = ProgressStore.Create(campaign); valid.completed = 4; ProgressStore.Save(valid, path);
                    valid.completed = 5; foreach (var t in valid.terminal) t.solved = true;
                    var mission = campaign.missions.First(m => m.id == "idor"); var state = new WebProgress { missionId = mission.id, kind = mission.kind };
                    var lab = new WebLabSession(mission.web, state);
                    lab.Request("https://" + mission.web.host + mission.web.path + "?id=" + mission.web.targetId); lab.ApplyPatch();
                    if (invalidUrl) state.attackUrl = "broken"; else state.attackPassword = null;
                    valid.web = new[] { state }; ProgressStore.Save(valid, path);
                    Check(ProgressStore.Load(campaign, path).completed == 4, "Invalid captured " + (invalidUrl ? "URL" : "body") + " recovers usable backup");
                }
                finally { DeleteReviewSave(path); }
            }
        }
        public static void VerifySocFilterEvidence(Campaign campaign)
        {
            var scenario = campaign.missions.First(m => m.kind == "soc").soc;
            var state = new SocProgress(); var soc = new SocSession(scenario, state);
            foreach (var a in scenario.alerts) soc.Triage(a.id, a.truePositive, out _);
            var attack = scenario.attacks[0]; soc.ApplyFilter("ip.src == " + scenario.attackerIp); soc.SelectPacket(attack.packetIds[0]);
            soc.IdentifyAttacker(scenario.attackerIp, out _); soc.ApplyFilter("ip.src = wrong");
            Check(!soc.Classify(attack.kind, out _) && state.classified.Length == 0, "Malformed filter cannot classify stale evidence");
            soc.ApplyFilter("ip.src == " + scenario.attackerIp); soc.SelectPacket(attack.packetIds[0]); soc.ApplyFilter("dns");
            Check(!soc.Classify(attack.kind, out _) && state.classified.Length == 0, "Filtered-out evidence cannot advance classification");
        }
        static string ReviewSavePath()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "CyberCityVerification"); Directory.CreateDirectory(directory);
            return Path.Combine(directory, "review-" + Guid.NewGuid().ToString("N") + ".json");
        }
        static void DeleteReviewSave(string path)
        { foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix); }

        [MenuItem("Tools/CyberCity/Verify interface edge cases")]
        public static void VerifyInterfaceEdgeCases()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            game = UnityEngine.Object.FindAnyObjectByType<CyberCityGame>();
            var failures = new List<string>();
            Action<bool, string> expect = (valid, label) => { if (valid) checks.Add(label); else failures.Add(label); };
            var current = game.Current; string title = current?.title;
            var mission = game.Campaign.missions.First(m => m.kind == "idor"); string patchLabel = mission.web.patchLabel; var hints = mission.web.hints;
            try
            {
                if (current != null) current.title = "Patrol for {player}";
                game.UI.RefreshHud(); expect(!VisibleText().Any(t => t.Contains("{player}")), "Dynamic HUD resolves player token");
                if (current != null) current.title = title;
                game.UI.RefreshHud();
                mission.web.patchLabel = "{player}: ownership check"; mission.web.hints = new[] { "{player}, compare the owner.", "{player}, inspect the other training ID." };
                var state = new WebProgress { missionId = mission.id, kind = mission.kind, hints = 2 };
                new FakeBrowser(game.UI, mission, mission.web, state, () => { }).Show();
                expect(!VisibleText().Any(t => t.Contains("{player}")), "Refreshed browser source labels resolve player token");
                var review = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).FirstOrDefault(b => b.GetComponentInChildren<Text>()?.text.StartsWith("REVIEW HINTS", StringComparison.Ordinal) == true);
                expect(review != null && review.interactable, "Previously paid browser hints can be reviewed after reopen");
                if (review != null && review.interactable)
                {
                    int paid = game.Progress.hintUses.Sum(); review.onClick.Invoke();
                    expect(VisibleText().Any(t => t.Contains("compare the owner") && t.Contains("other training ID")) && !VisibleText().Any(t => t.Contains("{player}")), "Unlocked hints render together with personalized text");
                    expect(game.Progress.hintUses.Sum() == paid, "Reviewing unlocked hints is free");
                }
                var socMission = game.Campaign.missions.First(m => m.kind == "soc");
                var fixture = JsonUtility.FromJson<Mission>(JsonUtility.ToJson(socMission));
                fixture.soc.alerts = Enumerable.Range(1, 8).Select(i => new SocAlert { id = "review-" + i, title = "Review alert " + i, context = "Approved training evidence", packetIds = fixture.soc.alerts[0].packetIds, truePositive = true }).ToArray();
                fixture.soc.Validate(); new SocUI(game.UI, fixture, new SocProgress { missionId = fixture.id }, () => { }).Show();
                var queue = UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsInactive.Exclude).FirstOrDefault(s => s.gameObject.name == "Alert queue");
                expect(queue != null && queue.content.rect.height > queue.viewport.rect.height && queue.content.GetComponentsInChildren<Button>().Length == 8, "Accepted eight-alert SOC packs have a scrollable queue");
                if (queue != null)
                {
                    Canvas.ForceUpdateCanvases(); queue.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                    var last = queue.content.GetComponentsInChildren<Button>().Last(); var corners = new Vector3[4]; queue.viewport.GetWorldCorners(corners);
                    Vector3 center = last.GetComponent<RectTransform>().TransformPoint(last.GetComponent<RectTransform>().rect.center);
                    expect(center.y >= corners[0].y && center.y <= corners[1].y, "Last alert scrolls into the visible viewport");
                }
            }
            finally
            {
                if (current != null) current.title = title; mission.web.patchLabel = patchLabel; mission.web.hints = hints;
                game.UI.RefreshHud(); if (game.Started) game.UI.CloseModal(); else game.UI.ShowStart();
            }
            if (failures.Count > 0) throw new Exception("Interface regressions: " + string.Join("; ", failures));
        }

        [MenuItem("Tools/CyberCity/Verify complete playthrough")]
        public static void StartPlaythrough()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            if (running) throw new InvalidOperationException("Verification is already running.");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            VerifyCore();
            game = UnityEngine.Object.FindAnyObjectByType<CyberCityGame>();
            Check(game != null && game.Campaign != null, "Runtime game initialized");
            saveBackup.Clear();
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string path = ProgressStore.SavePath + suffix;
                saveBackup[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            failure = null; actions.Clear(); running = true; Status = "Running full playthrough";
            EditorApplication.playModeStateChanged += RestoreAfterPlay;
            ConfigureVerificationInput();
            Add(() => game.NewPatrol());
            Add(() => { Click("BEGIN YOUR PATROL"); Check(!game.Started, "Username required before patrol"); FindInput().text = "  Byte_42  "; Click("BEGIN YOUR PATROL"); });
            Add(() => Check(game.Started && game.Progress.playerName == "Byte_42", "Valid username is trimmed and persisted"));
            Add(() => game.UI.ShowPause());
            Add(() => Click("Change username"));
            Add(() => { FindInput().text = "ab"; Click("SAVE USERNAME"); Check(game.Progress.playerName == "Byte_42", "Invalid pause rename preserves stored identity"); });
            Add(() => { FindInput().text = "Ada_Byte"; Click("SAVE USERNAME"); Check(game.Progress.playerName == "Ada_Byte", "Pause rename applies valid username"); });
            Add(() => { game.UI.ShowStart(); Check(VisibleText().Any(t => t.Contains("WELCOME BACK, Ada_Byte")), "Returning player sees saved name"); Click("CONTINUE PATROL"); });
            Add(VerifyInterfaceEdgeCases);
            Add(() => Capture("02-city"));
            Add(() => { movementStart = game.player.transform.position; Press(Key.W); });
            Add(() => { var position = game.player.transform.position; Debug.Log("Movement probe: " + movementStart + " -> " + position + "; enabled=" + game.player.CanMove + "; key=" + Keyboard.current.wKey.isPressed + "; keyboard enabled=" + Keyboard.current.enabled + "; velocity=" + game.player.GetComponent<Rigidbody2D>().linearVelocity); Release(); Check(position.y > movementStart.y + .25f, "WASD input moves Rigidbody2D"); });
            Add(() => { game.player.GetComponent<Rigidbody2D>().position = WorldLayout.FromPixel(180,407); Press(Key.S); });
            Add(() => { }); Add(() => { }); Add(() => {
                Release(); Check(game.player.transform.position.y > -14.4f, "Water collision blocks movement from the bridge");
                game.player.GetComponent<Rigidbody2D>().position = WorldLayout.Spawn;
            });
            Add(() => {
                Check(game.citizens.Length == 7, "Seven main NPCs present");
                var blue = game.citizens.First(n => n.citizenName == "Blue"); game.Interact(blue);
            });
            Add(() => {
                Check(VisibleText().Any(t => t.Contains("still locked")), "Later mentor content is locked"); game.UI.CloseModal();
            });
            Add(() => VerifyReachability());
            Add(() => { CyberCityWorldVerification.VerifyProgression(game); Check(true, "48 area/progression combinations and actual door gates"); });
            Add(() => {
                game.Progress.completed = 2;
                foreach (var area in game.areas) area.Initialize(game);
                game.player.GetComponent<Rigidbody2D>().position = WorldLayout.FromPixel(214,376);
            });
            Add(() => { Check(game.DoorIsNearest && game.NearestDoor.areaName == "Internet Cafe", "Cafe entrance takes priority over a farther NPC"); Press(Key.E); });
            Add(() => { Release(); Check(VisibleText().Any(t => t.Contains(game.Current.title)) && FindButton("START MISSION") != null, "E at Cafe door routes to current mentor Maya"); game.UI.CloseModal(); });
            Add(() => {
                game.Progress.completed = 0;
                foreach (var area in game.areas) area.Initialize(game);
                game.player.GetComponent<Rigidbody2D>().position = WorldLayout.Spawn; game.UI.RefreshHud();
            });
            Add(() => {
                // Emulate the nonserialized event delegate loss of Unity's script reload.
                typeof(CyberCityGame).GetField("ProgressChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(game, null);
                game.StartCoroutine("RestoreInterface");
            });
            Add(() => { game.UI.CloseModal(); Check(game.areas.All(a => a.IsOpen == (game.Progress.completed >= a.UnlockAfter)), "Reload path silently restores area visuals and subscriptions"); });
            foreach (var resolution in new[] { new Vector2Int(1600,900), new Vector2Int(2048,1152), new Vector2Int(2560,1440), new Vector2Int(3840,2160), new Vector2Int(1920,1080) })
            {
                var size = resolution;
                Add(() => CyberCityWorldVerification.SetGameViewResolution(size.x, size.y));
                Add(() => { CyberCityWorldVerification.VerifyFraming(game, size.x, size.y); Check(true, "Whole map fits outside HUD at " + size); });
            }
            Add(() => Capture("world-plaza-start"));
            AddWorldCapture("academy-open", 498, 205);
            AddWorldCapture("internet-cafe-locked", 218, 411);
            AddWorldCapture("linux-lab-locked", 746, 260);
            AddWorldCapture("web-lab-locked", 817, 409);
            AddWorldCapture("player-house", 220, 245);
            AddWorldCapture("cyber-corp", 499, 499);
            AddWorldCapture("bridge-west", 180, 407);
            AddWorldCapture("bridge-east", 738, 413);
            AddWorldCapture("edge-south", 666, 495);
            for (int i = 0; i < game.Campaign.missions.Length; i++)
            {
                int index = i; Mission mission = game.Campaign.missions[index];
                Add(() => { game.UI.CloseModal(); Check(game.Progress.completed == index, "Mission " + (index+1) + " unlocks in order"); game.player.GetComponent<Rigidbody2D>().position = (Vector2)game.Target.transform.position + Vector2.down; });
                if (mission.kind != "quiz" && mission.kind != "evidence" && mission.kind != "graduation")
                    Add(() => { game.CompleteMission(); Check(game.Progress.completed == index, "Practical evidence gates mission completion: " + mission.id); });
                Add(() => game.Interact(game.Target));
                Add(() => Click("START MISSION"));
                if (mission.kind == "evidence")
                {
                    Add(() => { Check(!FindButton("HIGHLIGHT").interactable, "Evidence requires highlights before verdict: " + mission.id); Capture("03-" + mission.id); });
                    foreach (var item in mission.evidence.Where(e => e.suspicious)) { string label = item.label; Add(() => Click(label)); }
                    Add(() => Click("REPORT EVIDENCE"));
                }
                else if (mission.kind == "terminal")
                {
                    Add(() => Capture("04-terminal"));
                    foreach (string cmd in new[] { "ls -a", "cd .archive", "cat access.txt", "submit CYBER{verify_first}" })
                    { string value = cmd; Add(() => { FindInput().text = value; Click("RUN"); }); }
                    Add(() => game.UI.CloseModal());
                    Add(() => game.Interact(game.Target));
                    Add(() => Click("START MISSION"));
                    Add(() => { Check(VisibleText().Any(t => t.Contains("FLAGS 1/3")), "Earned flag remains after reopening terminal UI"); Click("02  Log hunt"); });
                    foreach (string cmd in new[] { "find . -name auth.log", "grep Failed logs/auth.log", "submit CYBER{203.0.113.42}" })
                    { string value = cmd; Add(() => { FindInput().text = value; Click("RUN"); }); }
                    Add(() => Click("03  Least privilege"));
                    foreach (string cmd in new[] { "whoami", "ls -l private", "cat private/flag.txt", "chmod 400 private/flag.txt", "cat private/flag.txt", "submit CYBER{owner_read_only}" })
                    { string value = cmd; Add(() => { FindInput().text = value; Click("RUN"); }); }
                    Add(() => Click("ALL FLAGS VERIFIED"));
                }
                else if (mission.kind == "idor" || mission.kind == "xss" || mission.kind == "sqli") AddWebRoute(mission, mission.web);
                else if (mission.kind == "soc") AddSocRoute(mission);
                else if (mission.kind == "final")
                {
                    foreach (var lab in mission.finalLabs) AddWebRoute(mission, lab);
                    AddSocRoute(mission);
                }
                else if (mission.kind == "graduation") Add(() => { Capture("09-graduation"); Click("TAKE THE FINAL PLEDGE"); });
                for (int n = 0; n < (mission.questions?.Length ?? 0); n++)
                {
                    int question = n;
                    if (index == 0 && n == 0)
                    {
                        Add(() => Click("A   "));
                        Add(() => Check(game.Progress.attempts[0] == 1 && game.Progress.completed == 0, "Wrong answer gives feedback without advancing"));
                    }
                    Add(() => Click(((char)('A' + mission.questions[question].answer)) + "   "));
                    if (index == 0 && n == 0) Add(() => Capture("06-feedback"));
                    Add(() => Click(question == mission.questions.Length-1 ? "COMPLETE MISSION" : "NEXT DECISION"));
                }
                Add(() => {
                    Check(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude).All(t => !t.supportRichText), "UI renders plain text: " + mission.id);
                    Check(!VisibleText().Any(t => t.Contains("{player}")), "Player tokens resolved: " + mission.id);
                    Check(game.Progress.completed == index+1, "Mission " + (index+1) + " completes through its actual UI");
                    Check(ProgressStore.Load(game.Campaign).completed == index+1, "Mission " + (index+1) + " persists immediately");
                });
                if (index == 0 || index == 3 || index == 4)
                {
                    string areaName = index == 0 ? "Internet Cafe" : index == 3 ? "Linux Lab" : "Web Security Lab";
                    string slug = index == 0 ? "internet-cafe" : index == 3 ? "linux-lab" : "web-lab";
                    Add(() => game.UI.CloseModal());
                    Add(() => {
                        var area = game.areas.Single(a => a.areaName == areaName);
                        Check(area.IsOpen && area.IsAnimating && area.UnlockAnimations == 1, "Mission event animates unlock once: " + areaName);
                        Capture("world-" + slug + "-unlock");
                    });
                    Add(() => { });
                    Add(() => {
                        var area = game.areas.Single(a => a.areaName == areaName);
                        Check(area.IsOpen && !area.IsAnimating && area.glow.enabled && !area.lockIcon.enabled, "Unlock settles into open state: " + areaName);
                        Capture("world-" + slug + "-open");
                    });
                    Add(() => game.UI.ShowMap());
                    Add(() => { Check(GameObject.Find("Active mission building") != null, "Atlas marks next mission building"); Capture("world-atlas-" + slug); game.UI.CloseModal(); });
                }
            }
            Add(() => { game.UI.CloseModal(); game.player.GetComponent<Rigidbody2D>().position = WorldLayout.Spawn; });
            Add(() => Capture("world-all-open"));
            Add(() => {
                Check(game.Progress.completed == game.Campaign.missions.Length && game.Progress.score == ProgressStore.MaximumScore(game.Campaign) - ProgressStore.RetryPenalty, "Complete campaign scores once with retry penalty");
                Check(game.Current == null && game.Target == null, "Final campaign state has no pending mission");
                int completedScore = game.Progress.score; game.CompleteMission();
                Check(game.Progress.score == completedScore, "Completed campaign cannot grant XP twice");
                game.UI.ShowEnding();
            });
            Add(() => Capture("07-completion"));
            Add(() => {
                Status = "PASS: " + checks.Count + " checks including all missions through runtime UI";
                WriteReport(); Debug.Log("CyberCity " + Status); End();
            });
            next = Time.unscaledTimeAsDouble + .5; nextFrame = Time.frameCount + 2; EditorApplication.update += Tick;
        }

        static void AddWebRoute(Mission mission, WebScenario scenario)
        {
            Add(() => {
                Check(!FindButton("APPLY PATCH").interactable, "Source patch waits for observed attack: " + mission.id + "/" + scenario.kind);
                if (scenario.kind == "idor") FindInput("Browser address").text = "https://" + scenario.host + scenario.path + "?id=" + scenario.targetId;
                else FindInput("Browser form").text = scenario.kind == "xss" ? "<script>alert(1)</script>" : "admin'--";
                Click("GO");
            });
            Add(() => { Click("DevTools / Network"); Capture("05-" + mission.id + "-" + scenario.kind); });
            Add(() => Click("ENABLE FIX:"));
            Add(() => Click("APPLY PATCH"));
            Add(() => { Check(!game.WebState(mission, scenario.kind).verified, "Web patch cannot skip retest: " + mission.id); Click("RETEST SAME REQUEST"); });
            Add(() => Capture("05-" + mission.id + "-" + scenario.kind + "-patched"));
            Add(() => Click("CONTINUE TO DEBRIEF"));
        }

        static void AddSocRoute(Mission mission)
        {
            var scenario = mission.soc;
            foreach (var alert in scenario.alerts)
            {
                string id = alert.id; bool verdict = alert.truePositive;
                Add(() => ClickNamed("Alert-" + id));
                Add(() => Click(verdict ? "TRUE POSITIVE" : "FALSE POSITIVE"));
            }
            Add(() => { FindInput("SOC filter").text = "ip.src = wrong"; Click("APPLY FILTER"); Check(VisibleText().Any(t => t.Contains("Filter syntax")), "Malformed SOC filter has inline feedback"); });
            Add(() => { FindInput("SOC filter").text = "ip.src == " + scenario.attackerIp; Click("APPLY FILTER"); });
            Add(() => { ClickNamed("Packet-" + scenario.attacks[0].packetIds[0]); Capture("08-soc-traffic"); });
            Add(() => { FindInput("SOC attacker").text = scenario.attackerIp; Click("SET ATTACKER"); });
            foreach (var attack in scenario.attacks)
            {
                string packet = attack.packetIds[0], kind = attack.kind;
                Add(() => ClickNamed("Packet-" + packet)); Add(() => Click("TAG " + kind));
            }
            foreach (string label in new[] { "[ ] BLOCK IP", "[ ] PATCH APPS", "[ ] RESET ACCOUNTS" })
            { string button = label; Add(() => Click(button)); }
            Add(() => Click("APPLY RESPONSE"));
            Add(() => Click("OPEN REPORT TEMPLATE"));
            Add(() => {
                FindInput("Report-who").text = scenario.attackerIp;
                FindInput("Report-what").text = string.Join(", ", scenario.attacks.Select(a => a.kind));
                FindInput("Report-when").text = scenario.firstSeen;
                FindInput("Report-impact").text = "Private profiles and accounts exposed.";
                FindInput("Report-mitigation").text = "Block source, patch apps, reset credentials.";
                Capture("08-soc-report");
            });
            Add(() => Click("SUBMIT INCIDENT REPORT"));
        }

        static void ClickNamed(string name)
        {
            var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).First(b => b.gameObject.name == name);
            if (!button.interactable) throw new Exception("Button disabled: " + name); button.onClick.Invoke();
        }

        static void Add(Action action) { actions.Enqueue(action); }
        static void AddWorldCapture(string name, float x, float y)
        {
            Add(() => { game.UI.CloseModal(); game.player.GetComponent<Rigidbody2D>().position = WorldLayout.FromPixel(x, y); });
            Add(() => Capture("world-" + name));
        }
        static void Tick()
        {
            // Editor wall time advances during screenshot/import stalls while no player loop runs.
            // Give input, physics, coroutines and deferred UI destruction actual game frames.
            if (!running || Time.unscaledTimeAsDouble < next || Time.frameCount < nextFrame) return;
            next = Time.unscaledTimeAsDouble + .4; nextFrame = Time.frameCount + 2;
            try { if (actions.Count > 0) actions.Dequeue()(); }
            catch (Exception e) { failure = e.ToString(); Status = "FAILED: " + e.Message; WriteReport(); Debug.LogError(Status); End(); }
        }
        static void End()
        {
            Release(); RestoreVerificationInput(); running = false; EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
        }
        static void RestoreAfterPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            RestoreVerificationInput();
            foreach (var pair in saveBackup)
            {
                if (pair.Value != null) File.WriteAllBytes(pair.Key, pair.Value);
                else if (File.Exists(pair.Key)) File.Delete(pair.Key);
            }
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= RestoreAfterPlay; running = false;
        }
        static void ConfigureVerificationInput()
        {
            // Editor input buffers differ from runtime buffers when the desktop is unfocused.
            // Clone settings for this run and use a virtual keyboard; no project/user input settings are saved.
            originalInputSettings = InputSystem.settings;
            verificationInputSettings = UnityEngine.Object.Instantiate(originalInputSettings);
            verificationInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            verificationInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = verificationInputSettings;
            originalKeyboard = Keyboard.current;
            pausedKeyboards.Clear();
            foreach (var keyboard in InputSystem.devices.OfType<Keyboard>().Where(k => k.enabled).ToArray())
            { pausedKeyboards.Add(keyboard); InputSystem.DisableDevice(keyboard); }
            verificationKeyboard = InputSystem.AddDevice<Keyboard>("CyberCity verification keyboard");
            verificationKeyboard.MakeCurrent();
        }
        static void RestoreVerificationInput()
        {
            if (verificationKeyboard != null && verificationKeyboard.added) InputSystem.RemoveDevice(verificationKeyboard);
            verificationKeyboard = null;
            foreach (var keyboard in pausedKeyboards) if (keyboard.added) InputSystem.EnableDevice(keyboard);
            pausedKeyboards.Clear();
            if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            originalKeyboard = null;
            if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
            originalInputSettings = null;
            if (verificationInputSettings != null) UnityEngine.Object.DestroyImmediate(verificationInputSettings);
            verificationInputSettings = null;
        }
        static void Press(Key key) { verificationKeyboard.MakeCurrent(); InputSystem.QueueStateEvent(verificationKeyboard, new KeyboardState(key)); }
        static void Release() { if (verificationKeyboard != null && verificationKeyboard.added) InputSystem.QueueStateEvent(verificationKeyboard, new KeyboardState()); }
        static Button FindButton(string prefix)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)
                .FirstOrDefault(b => b.GetComponentInChildren<Text>()?.text.StartsWith(prefix, StringComparison.Ordinal) == true)
                ?? throw new Exception("Button not found: " + prefix);
        }
        static void Click(string prefix)
        {
            var b = FindButton(prefix); if (!b.interactable) throw new Exception("Button disabled: " + prefix); b.onClick.Invoke();
        }
        static InputField FindInput(string objectName = null) => UnityEngine.Object.FindObjectsByType<InputField>(FindObjectsInactive.Exclude)
            .FirstOrDefault(i => objectName == null || i.gameObject.name == objectName) ?? throw new Exception("Input not found: " + objectName);
        static IEnumerable<string> VisibleText() => UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude).Select(t => t.text);
        static void Capture(string name)
        {
            Directory.CreateDirectory("Documentation/Screenshots");
            ScreenCapture.CaptureScreenshot("Documentation/Screenshots/" + name + ".png");
        }
        static void WriteReport()
        {
            Directory.CreateDirectory("Documentation");
            var lines = new List<string> { "CyberCity verification", Status, "UTC: " + DateTime.UtcNow.ToString("O"), "" };
            lines.AddRange(checks.Select(c => "PASS  " + c)); if (failure != null) lines.Add(failure);
            File.WriteAllLines("Documentation/verification.txt", lines);
        }

        static void VerifyReachability()
        {
            // Check a half-unit navigation grid against actual scene colliders, ignoring only the player.
            Physics2D.SyncTransforms();
            var start = Vector2Int.RoundToInt(WorldLayout.Spawn * 2); var visited = new HashSet<Vector2Int> { start }; var queue = new Queue<Vector2Int>(); queue.Enqueue(start);
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while(queue.Count>0)
            {
                var node = queue.Dequeue();
                foreach(var direction in directions)
                {
                    var nextNode = node + direction;
                    if(nextNode.x<1||nextNode.x>WorldLayout.Width*2-1||nextNode.y>-1||nextNode.y<-WorldLayout.Height*2+1||visited.Contains(nextNode))continue;
                    var colliders=Physics2D.OverlapCircleAll((Vector2)nextNode*.5f+Vector2.up*.16f,.22f);
                    if(colliders.Any(c=>c.GetComponent<PlayerController>()==null))continue;
                    visited.Add(nextNode);queue.Enqueue(nextNode);
                }
            }
            foreach(var citizen in game.citizens)
                Check(visited.Any(p=>Vector2.Distance((Vector2)p*.5f,citizen.transform.position)<1.45f), "Walkable route to " + citizen.citizenName);
            foreach(var area in game.areas)
                Check(visited.Any(p=>Vector2.Distance((Vector2)p*.5f,area.door.position)<1.45f), "Walkable route to door / " + area.areaName);
            foreach(var waypoint in new[] { WorldLayout.FromPixel(180,407), WorldLayout.FromPixel(738,413), WorldLayout.FromPixel(666,495) })
                Check(visited.Any(p=>Vector2.Distance((Vector2)p*.5f,waypoint)<.75f), "Walkable bridge/edge route " + waypoint);
        }
    }
}
#endif
