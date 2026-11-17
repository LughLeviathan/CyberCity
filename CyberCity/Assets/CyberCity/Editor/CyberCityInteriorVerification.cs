#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Reflection;

namespace CyberCity.Editor
{
    public static class CyberCityInteriorVerification
    {
        static readonly List<string> checks = new List<string>();
        static readonly Dictionary<string,byte[]> backups = new Dictionary<string,byte[]>();
        const string Report = "Documentation/RevisionEvidence/interior-tests.txt";
        public static string Status { get; private set; } = "Not run";
        static void Check(bool ok, string description)
        {
            if (!ok) throw new InvalidOperationException("REVISION FAIL: " + description);
            checks.Add("PASS " + description);
        }

        [MenuItem("Tools/CyberCity/Verify interior and arcade core")]
        public static void VerifyCore()
        {
            checks.Clear();
            string source=Resources.Load<TextAsset>("campaign").text;
            string expected=Resources.Load<TextAsset>("campaign_hash").text.Trim();
            Check(CampaignLoader.BundledHash(source)==expected,"Actual bundled campaign verifies");
            string lf=source.Replace("\r\n","\n");
            Check(CampaignLoader.BundledHash(lf)==CampaignLoader.BundledHash(lf.Replace("\n","\r\n")),"LF and CRLF hash identically for bundle only");
            Check(CampaignLoader.Hash(lf)!=CampaignLoader.Hash(lf.Replace("\n","\r\n")),"Optional pack hashing remains byte-exact");
            Check(CampaignLoader.BundledHash(lf.Replace("Professor Byte","Professor Bite"))!=expected,"Content changes still fail integrity");
            var campaign=CampaignLoader.Load();
            Check(campaign.missions.Length==11,"Existing eleven-mission campaign order retained");
            var data=JsonUtility.FromJson<MentorConversations>(Resources.Load<TextAsset>("npc_dialogue").text);
            string[] names={"Professor Byte","Alex","Maya","Detective Byte","SysAdmin Tux","Red","Blue"};
            Check(data.mentors.Length==7 && names.All(n=>data.mentors.Count(m=>m.npc==n)==1),"Seven unique conversations map to existing NPC names");
            foreach (var line in data.mentors)
                Check(new[] {line.greetingId,line.greetingEn,line.replyId,line.replyEn,line.lessonId,line.lessonEn}.All(v=>!string.IsNullOrWhiteSpace(v)),"NPC and MC lines in Indonesian and English: " + line.npc);
            foreach (string name in names)
            {
                string path=InteriorRoom.ScenePath(name);
                Check(File.Exists(path) && EditorBuildSettings.scenes.Any(s=>s.path==path && s.enabled),"Room exists and ships in build: " + name);
            }
            Check(Resources.Load<Sprite>("Interiors/virus")!=null,"Pixel virus sprite bundled");
            var run=new VirusRun();
            run.Step(.05f,Vector2.one,Vector2.up,false);
            Check(Vector2.Distance(run.Player,new Vector2(VirusRun.Width/2,VirusRun.Height/2))<11.01f,"Diagonal movement normalized");
            run.Paused=true; var player=run.Player; float elapsed=run.Elapsed;
            run.Step(.05f,Vector2.one,Vector2.up,true);
            Check(run.Player==player && run.Elapsed==elapsed && run.Shots.Count==0,"Pause freezes movement, timers and fire");
            run.Paused=false;
            for(int i=0;i<400;i++) run.Step(.05f,Vector2.left,Vector2.up,false);
            Check(run.Player.x>=20 && run.Player.y>=20,"Arena movement remains in bounds");
            Check(VirusRun.SegmentDistance(Vector2.zero,new Vector2(100,0),new Vector2(50,10))==10,"Swept shot collision prevents tunnelling");
            var win=new VirusRun();
            for(int i=0;i<5000 && !win.Finished;i++) win.Step(.02f,Vector2.zero,win.AutoAim(),true);
            Check(win.Won && win.Kills==24 && win.Wave==3,"Real firing clears all 24 viruses across three waves");
            int kills=win.Kills; elapsed=win.Elapsed; win.Step(.05f,Vector2.one,Vector2.up,true);
            Check(win.Kills==kills && win.Elapsed==elapsed,"Victory cannot advance or grant additional kills");
            var loss=new VirusRun();
            for(int i=0;i<5000 && !loss.Finished;i++) loss.Step(.02f,Vector2.zero,Vector2.zero,false);
            Check(loss.Finished && !loss.Won && loss.Health==0,"Contact damage leads to loss, not victory");
            var retry=new VirusRun(); Check(retry.Health==3 && retry.Kills==0 && retry.Wave==1,"Retry starts clean");
            string folder=Path.Combine(Path.GetTempPath(),"CyberCityRevision-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                string path=Path.Combine(folder,"save.json"); var save=ProgressStore.Create(campaign); save.playerName="Test_Byte";
                save.completed=1; save.score=150;
                Check(ProgressStore.Save(save,path),"Legacy-compatible save writes");
                string legacy=File.ReadAllText(path);
                foreach(string field in new[]{"virusAccepted","virusCleared","virusReported","virusBestKills"})
                    legacy=System.Text.RegularExpressions.Regex.Replace(legacy,"\\s*\\\""+field+"\\\"\\s*:\\s*(false|0),","");
                File.WriteAllText(path,legacy);
                var loaded=ProgressStore.Load(campaign,path);
                Check(loaded.playerName=="Test_Byte" && loaded.version==2 && loaded.completed==1 && loaded.score==150 && !loaded.virusAccepted,"Old v2 save without new fields keeps earned XP and campaign progress");
                save.virusAccepted=true; save.virusCleared=true; save.virusReported=true; save.virusBestKills=24;
                Check(ProgressStore.Save(save,path),"Special quest saves"); loaded=ProgressStore.Load(campaign,path);
                Check(loaded.virusReported && loaded.virusBestKills==24 && loaded.score==150 && loaded.completed==1,"Badge persists without changing campaign XP or order");
                File.WriteAllText(path,JsonUtility.ToJson(new ProgressData { virusBestKills=999 }));
                Check(ProgressStore.Load(campaign,path).playerName=="Test_Byte","Malformed save recovers valid backup");
            }
            finally { Directory.Delete(folder,true); }
            Status="PASS: " + checks.Count + " revision core checks"; Write(); Debug.Log(Status);
        }

        [MenuItem("Tools/CyberCity/Verify interior play mode")]
        public static void VerifyPlayMode()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            var game=UnityEngine.Object.FindAnyObjectByType<CyberCityGame>();
            if (Status=="Running interior play mode") throw new InvalidOperationException("Already running.");
            if (game.Travel.IsInside || game.Travel.IsTransitioning) throw new InvalidOperationException("Return to the city before starting this verification.");
            backups.Clear();
            foreach(string suffix in new[]{"",".bak",".tmp"})
            {
                string path=ProgressStore.SavePath+suffix; backups[path]=File.Exists(path)?File.ReadAllBytes(path):null;
            }
            string snapshot=JsonUtility.ToJson(game.Progress); bool wasStarted=game.Started;
            Input("ConfigureVerificationInput");
            EditorApplication.playModeStateChanged+=OnPlayState;
            Status="Running interior play mode";
            game.StartCoroutine(Guard(Play(game),game,snapshot,wasStarted));
        }

        static IEnumerator Guard(IEnumerator test,CyberCityGame game,string snapshot,bool wasStarted)
        {
            while(true)
            {
                object next=null; bool more=false;
                try { more=test.MoveNext(); if(more) next=test.Current; }
                catch(Exception e) { Status="FAILED: "+e.Message; Debug.LogError(e); }
                if(!more) break;
                yield return next;
            }
            if (game.Travel.IsInside || game.Travel.IsTransitioning)
            {
                game.UI.CloseModal(); if (!game.Travel.IsTransitioning) game.Travel.Exit();
                float deadline=Time.realtimeSinceStartup+8;
                while(game.Travel.IsTransitioning && Time.realtimeSinceStartup<deadline) yield return null;
                if(game.Travel.IsTransitioning)
                {
                    Status="FAILED: Travel cleanup timed out; restoring files on return to Edit mode.";
                    Write(); EditorApplication.isPlaying=false; yield break;
                }
            }
            JsonUtility.FromJsonOverwrite(snapshot,game.Progress);
            foreach(var area in game.areas) area.Initialize(game);
            game.player.GetComponent<Rigidbody2D>().position=new Vector2(game.Progress.x,game.Progress.y);
            game.UI.CloseModal(); game.UI.RefreshHud();
            if(!wasStarted)
            {
                typeof(CyberCityGame).GetProperty("Started").SetValue(game,false); game.UI.ShowStart();
            }
            Input("RestoreVerificationInput"); RestoreFiles(); EditorApplication.playModeStateChanged-=OnPlayState;
            if(!Status.StartsWith("FAILED")) Status="PASS: "+checks.Count+" revision checks (core + play mode)";
            Write(); Debug.Log(Status);
        }

        static IEnumerator Play(CyberCityGame game)
        {
            game.NewPatrol(); game.SetPlayerName("Room_Tester",out _); game.Begin(); yield return null;
            Check(game.Campaign!=null && game.enabled,"No fatal checksum screen at startup");
            Check(game.citizens.All(n=>!n.gameObject.activeInHierarchy),"No outdoor NPCs");
            game.Interact(game.citizens[0]); Check(!game.UI.HasModal,"Remote outdoor NPC interaction rejected");
            var body=game.player.GetComponent<Rigidbody2D>();
            var red=game.citizens.Single(n=>n.citizenName=="Red");
            var web=game.areas.Single(a=>a.areaName==red.area); body.position=web.door.position; game.player.transform.position=body.position;
            game.Travel.Enter(red); Check(!game.Travel.IsTransitioning,"Locked room entry rejected");
            game.InteractArea(web); Check(game.UI.HasModal,"Locked door gives feedback"); game.UI.CloseModal();
            foreach(var npc in game.citizens)
            {
                game.Progress.completed=npc.unlockAfter;
                foreach(var area in game.areas) area.Initialize(game);
                var door=game.areas.Single(a=>a.areaName==npc.area);
                body.position=door.door.position+Vector3.down*.9f; game.player.transform.position=body.position;
                Physics2D.SyncTransforms();
                Check(!Physics2D.OverlapCircleAll(body.position+Vector2.up*.16f,.22f).Any(c=>c.GetComponent<PlayerController>()==null),"Door approach is walkable");
                var outside=body.position; game.InteractArea(door);
                Check(game.UI.HasModal,"Door opens room directory: "+npc.citizenName);
                var button=game.UI.Content.GetComponentsInChildren<Button>().Single(b=>b.name.Contains(npc.citizenName));
                button.onClick.Invoke();
                Check(game.Travel.IsTransitioning && !game.player.CanMove,"Entry fade locks input: "+npc.citizenName);
                float deadline=Time.realtimeSinceStartup+8;
                while(game.Travel.IsTransitioning && Time.realtimeSinceStartup<deadline) yield return null;
                Check(game.Travel.Room!=null && game.Travel.Room.citizenName==npc.citizenName,"Correct separate scene loaded: "+npc.citizenName);
                Check(npc.gameObject.scene.path==InteriorRoom.ScenePath(npc.citizenName) && game.citizens.Count(n=>n.gameObject.activeInHierarchy)==1,"Only resident visible: "+npc.citizenName);
                Check(!game.areas[0].gameObject.activeInHierarchy,"Outdoor scenery and colliders hidden indoors");
                Check(Vector2.Distance(body.position,game.Travel.Room.spawn)<.1f,"Interior spawn clear");
                VerifyReachability(game);
                Directory.CreateDirectory("Documentation/RevisionEvidence/Screenshots");
                ScreenCapture.CaptureScreenshot("Documentation/RevisionEvidence/Screenshots/room-"+npc.citizenName.Replace(" ","_")+".png");
                yield return null;
                Check(Vector2.Distance(game.Travel.SavePosition,outside)<.01f,"Indoor save keeps outdoor return point");
                game.Save(); Check(Mathf.Abs(game.Progress.x-outside.x)<.01f && Mathf.Abs(game.Progress.y-outside.y)<.01f,"Saved coordinates stay in city");
                body.position=game.Travel.Room.mentor+Vector2.down; game.player.transform.position=body.position;
                yield return null; game.Interact(npc);
                Check(game.UI.Content!=null && game.UI.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("Room_Tester")),"NPC dialogue personalizes player name");
                if(npc.citizenName=="Red") { ScreenCapture.CaptureScreenshot("Documentation/RevisionEvidence/Screenshots/dialogue-Red.png"); yield return null; }
                for(int step=0;step<3;step++)
                {
                    game.UI.Content.GetComponentsInChildren<Button>().First().onClick.Invoke(); yield return null;
                    if(step==0) Check(game.UI.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text=="Room_Tester / MC"),"MC speaks in conversation");
                }
                game.UI.Content.GetComponentsInChildren<Button>().First().onClick.Invoke();
                Check(game.UI.Content.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("START MISSION")),"Dialogue leads to existing mission briefing");
                game.UI.CloseModal();
                if(npc.citizenName=="Red")
                {
                    game.UI.ShowVirusBriefing(npc);
                    game.UI.Content.GetComponentsInChildren<Button>().First(b=>b.name.Contains("VIRUS SWEEP")).onClick.Invoke(); yield return null;
                    Check(game.UI.Arcade!=null,"Red starts special quest arena");
                    var arcade=game.UI.Arcade; arcade.TogglePause(); Vector2 p=arcade.Run.Player; float elapsed=arcade.Run.Elapsed;
                    yield return new WaitForSeconds(.12f);
                    Check(arcade.Run.Paused && arcade.Run.Elapsed==elapsed && arcade.Run.Player==p,"Arcade UI pause freezes simulation");
                    arcade.TogglePause();
                    yield return new WaitForSeconds(2f); Check(arcade.Run.Elapsed>elapsed,"Arcade resumes after pause");
                    ScreenCapture.CaptureScreenshot("Documentation/RevisionEvidence/Screenshots/virus-sweep.png"); yield return null;
                    Input("Press",Key.Space);
                    float combatDeadline=Time.realtimeSinceStartup+90;
                    while(!arcade.Run.Finished && Time.realtimeSinceStartup<combatDeadline) yield return null;
                    Input("Release"); yield return null;
                    Check(arcade.Run.Won && game.Progress.virusCleared && game.Progress.virusBestKills==24,"Held SPACE fires through actual UI and wins all three waves");
                    ScreenCapture.CaptureScreenshot("Documentation/RevisionEvidence/Screenshots/virus-victory.png"); yield return null;
                    game.UI.CloseModal(); yield return null;
                    Check(game.UI.Arcade==null && game.Travel.Occupant==npc,"Arcade result returns to same room");
                    int xp=game.Progress.score;
                    game.UI.ShowConversation(npc,3);
                    game.UI.Content.GetComponentsInChildren<Button>().Single(b=>b.name.Contains("Virus Sweep")).onClick.Invoke();
                    Check(game.Progress.virusReported && game.Progress.score==xp,"Reporting victory to Red grants badge without campaign XP duplication");
                    game.UI.CloseModal(); game.UI.ShowVirusBriefing(npc);
                    Check(game.UI.Content.GetComponentsInChildren<Button>().Any(b=>b.name.Contains("VIRUS SWEEP")),"Completed special quest can be replayed");
                    game.UI.CloseModal();
                }
                body.position=game.Travel.Room.exit; game.player.transform.position=body.position;
                Check(game.Travel.NearExit,"South exit reachable: "+npc.citizenName);
                game.Travel.Exit(); deadline=Time.realtimeSinceStartup+8;
                while(game.Travel.IsTransitioning && Time.realtimeSinceStartup<deadline) yield return null;
                Check(!game.Travel.IsInside && !SceneManager.GetSceneByPath(InteriorRoom.ScenePath(npc.citizenName)).isLoaded,"Room scene unloaded on exit");
                Check(Vector2.Distance(body.position,outside)<.1f,"Return position restored: expected="+outside+" actual="+body.position);
                Check(game.citizens.All(n=>!n.gameObject.activeInHierarchy),"No outdoor NPC leak");
            }
        }

        static void VerifyReachability(CyberCityGame game)
        {
            Physics2D.SyncTransforms();
            var start=Vector2Int.RoundToInt(game.Travel.Room.spawn*2);
            var seen=new HashSet<Vector2Int>{start}; var queue=new Queue<Vector2Int>(); queue.Enqueue(start);
            while(queue.Count>0)
            {
                var p=queue.Dequeue();
                foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                {
                    var n=p+d; if(n.x<10 || n.x>54 || n.y < -31 || n.y > -8 || seen.Contains(n)) continue;
                    if(Physics2D.OverlapCircleAll((Vector2)n*.5f+Vector2.up*.16f,.22f).Any(c=>c.GetComponent<PlayerController>()==null)) continue;
                    seen.Add(n); queue.Enqueue(n);
                }
            }
            Check(seen.Any(p=>Vector2.Distance((Vector2)p*.5f,game.Travel.Room.mentor)<1.4f),"Walkable route from spawn to resident");
            Check(seen.Any(p=>Vector2.Distance((Vector2)p*.5f,game.Travel.Room.exit)<1f),"Walkable route from spawn to exit");
        }

        static void Input(string method,params object[] args) => typeof(CyberCityVerification).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
        static void OnPlayState(PlayModeStateChange state) { if(state==PlayModeStateChange.EnteredEditMode) { Input("RestoreVerificationInput"); RestoreFiles(); EditorApplication.playModeStateChanged-=OnPlayState; } }
        static void RestoreFiles()
        {
            foreach(var item in backups) { if(item.Value==null) { if(File.Exists(item.Key)) File.Delete(item.Key); } else File.WriteAllBytes(item.Key,item.Value); }
            backups.Clear();
        }
        static void Write() { Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report,Status+"\n"+string.Join("\n",checks)); }
    }
}
#endif
