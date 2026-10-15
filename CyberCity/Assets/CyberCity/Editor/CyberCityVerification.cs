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
        static bool running;
        static CyberCityGame game;
        static Vector3 movementStart;
        static string failure;
        public static string Status { get; private set; } = "Not run";
        static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL: " + name);
            checks.Add(name);
        }

        [MenuItem("Tools/CyberCity/Verify core systems")]
        public static void VerifyCore()
        {
            checks.Clear();
            var campaign = CampaignLoader.Load();
            Check(campaign.missions.Length == 12, "Twelve validated offline missions");
            Check(campaign.missions.Select(m => m.id).Distinct().Count() == 12, "Unique mission IDs");
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
            string folder = Path.Combine(Application.temporaryCachePath, "CyberCityVerification"); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "progress.json");
            foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
            var first = new ProgressData { completed = 3, score = 450, x = 5, y = -12, started = true };
            Check(ProgressStore.Save(first, path), "Atomic save succeeds");
            var loaded = ProgressStore.Load(path);
            Check(loaded.completed == 3 && loaded.score == 450 && loaded.x == 5, "Save round trip preserves progress and position");
            first.completed = 4; first.score = 600; ProgressStore.Save(first, path);
            File.WriteAllText(path, "damaged");
            Check(ProgressStore.Load(path).completed == 3, "Damaged primary save recovers backup");
            File.WriteAllText(path + ".bak", "damaged");
            Check(ProgressStore.Load(path).completed == 0, "Damaged primary and backup recover fresh data");
            foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
            Status = "PASS: " + checks.Count + " core checks"; WriteReport(); Debug.Log("CyberCity " + Status);
        }

        [MenuItem("Tools/CyberCity/Verify complete playthrough")]
        public static void StartPlaythrough()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            if (running) throw new InvalidOperationException("Verification is already running.");
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
            Add(() => { game.NewPatrol(); game.UI.CloseModal(); });
            Add(() => Capture("02-city"));
            Add(() => { movementStart = game.player.transform.position; Press(Key.W); });
            Add(() => { Release(); Check(game.player.transform.position.y > movementStart.y + .25f, "WASD input moves Rigidbody2D"); });
            Add(() => { game.player.GetComponent<Rigidbody2D>().position = new Vector2(14,-18.3f); Press(Key.A); });
            Add(() => { }); Add(() => { }); Add(() => {
                Release(); Check(game.player.transform.position.x > 11.2f, "Water collision blocks movement from the bridge");
                game.player.GetComponent<Rigidbody2D>().position = new Vector2(14,-8.2f);
            });
            Add(() => {
                Check(game.citizens.Length == 7, "Seven main NPCs present");
                var blue = game.citizens.First(n => n.citizenName == "Blue"); game.Interact(blue);
            });
            Add(() => {
                Check(VisibleText().Any(t => t.Contains("still locked")), "Later mentor content is locked"); game.UI.CloseModal();
            });
            Add(() => VerifyReachability());
            for (int i = 0; i < 12; i++)
            {
                int index = i; Mission mission = game.Campaign.missions[index];
                Add(() => { game.UI.CloseModal(); Check(game.Progress.completed == index, "Mission " + (index+1) + " unlocks in order"); game.player.GetComponent<Rigidbody2D>().position = (Vector2)game.Target.transform.position + Vector2.down; });
                Add(() => game.Interact(game.Target));
                Add(() => Click("START MISSION"));
                if (mission.kind == "evidence")
                {
                    Add(() => { Check(!FindButton("HIGHLIGHT").interactable, "Evidence requires highlights before verdict: " + mission.id); Capture("03-" + mission.id); });
                    foreach (var item in mission.evidence.Where(e => e.suspicious)) { string label = item.label; Add(() => Click(label)); }
                    Add(() => Click("SUBMIT YOUR VERDICT"));
                }
                else if (mission.kind == "terminal")
                {
                    Add(() => Capture("04-terminal"));
                    foreach (string cmd in new[] { "pwd", "cat README.txt", "ls -a", "cd .archive", "cat access.txt", "submit " + VirtualTerminal.Flag })
                    { string value = cmd; Add(() => { FindInput().text = value; Click("RUN"); }); }
                }
                else if (mission.kind == "idor" || mission.kind == "xss" || mission.kind == "sqli")
                {
                    Add(() => {
                        Check(!FindButton("OBSERVE THE FLAW").interactable, "Web defense follows observation: " + mission.id);
                        FindInput().text = mission.kind == "idor" ? "/api/profile/1002" : mission.kind == "xss" ? "<b>hello</b>" : "' OR '1'='1"; Click("SIMULATE");
                    });
                    Add(() => Capture("05-" + mission.id));
                    Add(() => Click("SWITCH TO BLUE TEAM"));
                }
                for (int n = 0; n < mission.questions.Length; n++)
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
                    Check(game.Progress.completed == index+1, "Mission " + (index+1) + " completes through its actual UI");
                    Check(ProgressStore.Load().completed == index+1, "Mission " + (index+1) + " persists immediately");
                });
            }
            Add(() => {
                Check(game.Progress.completed == 12 && game.Progress.score == 1785, "Complete campaign scores once with retry penalty");
                Check(game.Current == null && game.Target == null, "Final campaign state has no pending mission");
                game.UI.ShowEnding();
            });
            Add(() => Capture("07-completion"));
            Add(() => {
                Status = "PASS: " + checks.Count + " checks including twelve missions through runtime UI";
                WriteReport(); Debug.Log("CyberCity " + Status); End();
            });
            next = EditorApplication.timeSinceStartup + .5; EditorApplication.update += Tick;
        }

        static void Add(Action action) { actions.Enqueue(action); }
        static void Tick()
        {
            if (!running || EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + .4;
            try { if (actions.Count > 0) actions.Dequeue()(); }
            catch (Exception e) { failure = e.ToString(); Status = "FAILED: " + e.Message; WriteReport(); Debug.LogError(Status); End(); }
        }
        static void End()
        {
            Release(); running = false; EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
        }
        static void RestoreAfterPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            foreach (var pair in saveBackup)
            {
                if (pair.Value != null) File.WriteAllBytes(pair.Key, pair.Value);
                else if (File.Exists(pair.Key)) File.Delete(pair.Key);
            }
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= RestoreAfterPlay; running = false;
        }
        static void Press(Key key) { InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key)); }
        static void Release() { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); }
        static Button FindButton(string prefix)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.GetComponentInChildren<Text>()?.text.StartsWith(prefix, StringComparison.Ordinal) == true)
                ?? throw new Exception("Button not found: " + prefix);
        }
        static void Click(string prefix)
        {
            var b = FindButton(prefix); if (!b.interactable) throw new Exception("Button disabled: " + prefix); b.onClick.Invoke();
        }
        static InputField FindInput() => UnityEngine.Object.FindAnyObjectByType<InputField>();
        static IEnumerable<string> VisibleText() => UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Select(t => t.text);
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
            var start = new Vector2Int(28, -16); var visited = new HashSet<Vector2Int> { start }; var queue = new Queue<Vector2Int>(); queue.Enqueue(start);
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while(queue.Count>0)
            {
                var node = queue.Dequeue();
                foreach(var direction in directions)
                {
                    var nextNode = node + direction;
                    if(nextNode.x<1||nextNode.x>55||nextNode.y>-1||nextNode.y<-43||visited.Contains(nextNode))continue;
                    var colliders=Physics2D.OverlapCircleAll((Vector2)nextNode*.5f+Vector2.up*.16f,.22f);
                    if(colliders.Any(c=>c.GetComponent<PlayerController>()==null))continue;
                    visited.Add(nextNode);queue.Enqueue(nextNode);
                }
            }
            foreach(var citizen in game.citizens)
                Check(visited.Any(p=>Vector2.Distance((Vector2)p*.5f,citizen.transform.position)<1.45f), "Walkable route to " + citizen.citizenName);
        }
    }
}
#endif
