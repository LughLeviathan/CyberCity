#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

namespace CyberCity.Editor
{
    public static class CyberCityBuilder
    {
        const string Root = "Assets/CyberCity";
        const string Art = Root + "/Generated";
        const string World = "Assets/CyberCityWorld/Assets/CyberCityWorld";
        public const string ScenePath = Root + "/Scenes/CyberCity.unity";

        [MenuItem("Tools/CyberCity/Build playable game")]
        public static void BuildGame()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building the scene.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Art); Directory.CreateDirectory(Root + "/Scenes");
            Sprite[][] characters = new Sprite[8][];
            for (int i = 0; i < characters.Length; i++) characters[i] = BuildCharacter(i);
            var controller = BuildAnimator(characters[0]);
            BuildAudio();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(World + "/Generated/CyberCityLobby.prefab");
            if (source == null) throw new FileNotFoundException("The existing city prefab is missing.");
            var world = (GameObject)PrefabUtility.InstantiatePrefab(source);
            world.name = "CyberCity / Existing world art";
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            var materialPath = Art + "/PixelUnlit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, materialPath); }
            foreach (var sr in world.GetComponentsInChildren<SpriteRenderer>()) sr.sharedMaterial = mat;
            foreach (var tm in world.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapRenderer>()) tm.sharedMaterial = mat;
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(UniversalAdditionalCameraData));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(14, -11, -10);
            var camera = cameraGo.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 12.5f;
            camera.rect = new Rect(0, 0, 1, 1); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = GameUI.Ink; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            camera.allowHDR = false; camera.allowMSAA = false;
            var pixel = cameraGo.AddComponent<PixelPerfectCamera>();
            pixel.assetsPPU = 32; pixel.refResolutionX = 960; pixel.refResolutionY = 768;
            pixel.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            cameraGo.AddComponent<WorldCamera>();
            var playerGo = Actor("Citizen / Player", characters[0][0], new Vector2(14, -8.2f), mat);
            playerGo.tag = "Player";
            var body = playerGo.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous; body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var capsule = playerGo.AddComponent<CapsuleCollider2D>(); capsule.size = new Vector2(.42f,.32f); capsule.offset = new Vector2(0,.16f);
            playerGo.AddComponent<Animator>().runtimeAnimatorController = controller;
            var player = playerGo.AddComponent<PlayerController>();
            var citizenRoot = new GameObject("Seven mentors");
            string[] names = { "Professor Byte", "Alex", "Maya", "Detective Byte", "SysAdmin Tux", "Red", "Blue" };
            string[] roles = { "Cyber awareness mentor", "Student ambassador", "Message investigator", "Digital detective", "Linux mentor", "Web security analyst", "SOC analyst" };
            string[] areas = { "Cyber Academy", "Internet Cafe", "Internet Cafe", "Internet Cafe", "Linux Lab", "Web Security Lab", "Web Security Lab" };
            Vector2[] positions = { new Vector2(14,-6), new Vector2(6.1f,-10.1f), new Vector2(5,-15.3f), new Vector2(8.9f,-12), new Vector2(23,-7.2f), new Vector2(22.2f,-15.2f), new Vector2(25.1f,-15.3f) };
            Color[] colors = { GameUI.White, GameUI.Violet, GameUI.Pink, GameUI.Gold, GameUI.Teal, GameUI.Pink, GameUI.Cyan };
            var campaign = CampaignLoader.Load();
            var citizens = new Citizen[7];
            for (int i = 0; i < 7; i++)
            {
                var go = Actor(names[i], characters[i + 1][0], positions[i], mat); go.transform.SetParent(citizenRoot.transform);
                var col = go.AddComponent<CircleCollider2D>(); col.radius = .23f; col.offset = new Vector2(0,.17f);
                var npc = go.AddComponent<Citizen>(); npc.citizenName = names[i]; npc.role = roles[i]; npc.area = areas[i];
                npc.accent = colors[i]; npc.unlockAfter = CampaignLoader.UnlockForNpc(campaign, names[i]); npc.idleFrames = new[] { characters[i + 1][0], characters[i + 1][3] };
                citizens[i] = npc;
            }
            AddBoundary("North boundary", new Vector2(14,.15f), new Vector2(29,.3f));
            AddBoundary("South boundary", new Vector2(14,-22.15f), new Vector2(29,.3f));
            AddBoundary("West boundary", new Vector2(-.15f,-11), new Vector2(.3f,23));
            AddBoundary("East boundary", new Vector2(28.15f,-11), new Vector2(.3f,23));
            var systems = new GameObject("CyberCity / Game systems");
            var ui = systems.AddComponent<GameUI>();
            ConfigureTexture("Assets/CyberCityWorld/Previews/lobby_unity.png");
            ui.cityPreview = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CyberCityWorld/Previews/lobby_unity.png");
            var game = systems.AddComponent<CyberCityGame>(); game.player = player; game.citizens = citizens; game.worldCamera = camera;
            game.music = systems.AddComponent<AudioSource>(); game.music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "/CityAmbience.wav"); game.music.loop = true; game.music.playOnAwake = false;
            game.effects = systems.AddComponent<AudioSource>(); game.effects.playOnAwake = false;
            game.clickSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "/Interface.wav");
            game.successSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "/Success.wav");
            game.errorSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "/Retry.wav");
            BuildAtlas();
            PlayerSettings.companyName = "Sijaw"; PlayerSettings.productName = "CyberCity";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = false;
            QualitySettings.vSyncCount = 1;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
            var prior = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).Select(s => new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(prior).ToArray();
            AssetDatabase.SaveAssets();
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.in2DMode = true;
                SceneView.lastActiveSceneView.LookAt(new Vector3(14,-11,0), Quaternion.identity, 15);
            }
            Debug.Log("CyberCity playable scene built: " + campaign.missions.Length + " missions, seven mentors, local campaign and saves.");
        }

        static GameObject Actor(string name, Sprite sprite, Vector2 position, Material material)
        {
            var go = new GameObject(name, typeof(SpriteRenderer)); go.transform.position = position;
            var sr = go.GetComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sharedMaterial = material;
            sr.sortingOrder = Mathf.RoundToInt(-position.y * 32) + 1; return go;
        }
        static void AddBoundary(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(BoxCollider2D)); go.transform.position = position; go.GetComponent<BoxCollider2D>().size = size;
        }

        static Sprite[] BuildCharacter(int index)
        {
            string path = Art + "/Citizen" + index + ".png";
            var texture = new Texture2D(96,96,TextureFormat.RGBA32,false); texture.SetPixels(new Color[96*96]);
            Color outline = GameUI.Ink, skin = GameUI.White, pants = GameUI.Hex("1F2A68");
            Color[] jackets = { GameUI.Cyan, GameUI.White, GameUI.Violet, GameUI.Pink, GameUI.Gold, GameUI.Teal, GameUI.Pink, GameUI.Cyan };
            Color jacket = jackets[index];
            for (int dir = 0; dir < 3; dir++)
            for (int frame = 0; frame < 4; frame++)
            {
                int ox = frame * 24, oy = dir * 32;
                Action<int,int,int,int,Color> fill = (x,y,w,h,color) => {
                    for (int xx=x; xx<x+w; xx++) for (int yy=y; yy<y+h; yy++)
                        texture.SetPixel(ox+xx, 95-(oy+yy), color);
                };
                int bob = frame == 1 || frame == 3 ? 1 : 0;
                fill(5,29,14,2,outline); fill(3,30,18,1,outline);
                int left = frame == 1 ? 1 : 0, right = frame == 3 ? 1 : 0;
                fill(7,23,4,6-left,outline); fill(13,23,4,6-right,outline);
                fill(8,23,2,4-left,pants); fill(14,23,2,4-right,pants);
                fill(7,28-left,4,2,skin); fill(13,28-right,4,2,skin);
                fill(6,14-bob,12,11,outline); fill(7,15-bob,10,9,jacket);
                fill(4,16-bob+left,3,8,outline); fill(17,16-bob+right,3,8,outline);
                fill(5,17-bob+left,2,5,jacket); fill(17,17-bob+right,2,5,jacket);
                fill(5,22-bob+left,2,2,skin); fill(17,22-bob+right,2,2,skin);
                fill(11,15-bob,2,8,pants); fill(8,23,8,2,outline); fill(11,23,2,1,GameUI.Gold);
                fill(6,4-bob,12,11,outline); fill(7,5-bob,10,9,skin);
                Color hair = index == 1 ? GameUI.Muted : pants;
                fill(6,4-bob,12,4,hair); fill(5,6-bob,2,5,hair);
                if (dir == 1) { fill(7,7-bob,10,6,hair); fill(8,13-bob,8,2,outline); }
                else if (dir == 2) { fill(7,7-bob,5,6,hair); fill(16,9-bob,3,3,skin); fill(15,9-bob,2,2,outline); }
                else
                {
                    fill(8,9-bob,2,2,outline); fill(14,9-bob,2,2,outline);
                    fill(10,13-bob,4,1,GameUI.Gold);
                    if (index == 0 || index == 1 || index == 5 || index == 7) fill(7,9-bob,10,2,GameUI.Cyan);
                }
                if (index == 4) { fill(5,5-bob,15,2,GameUI.Gold); fill(8,2-bob,9,4,GameUI.Gold); }
                if (index == 1) { fill(7,12-bob,2,3,GameUI.White); fill(15,12-bob,2,3,GameUI.White); }
                if (index == 3) fill(16,5-bob,3,10,GameUI.Pink);
                if (index == 6 || index == 7) { fill(10,17-bob,4,3,outline); fill(11,18-bob,2,1,GameUI.White); }
            }
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rectangles = new SpriteRect[12];
            for (int i=0; i<12; i++)
            {
                string name = "Citizen" + index + "_" + i.ToString("00");
                rectangles[i] = new SpriteRect { name=name, rect=new Rect(i%4*24,96-(i/4+1)*32,24,32),
                    alignment=SpriteAlignment.Custom,pivot=new Vector2(.5f,.1f),spriteID=existing.TryGetValue(name,out var id)?id:GUID.Generate() };
            }
            provider.SetSpriteRects(rectangles);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rectangles.Select(r => new SpriteNameFileIdPair(r.name,r.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        }

        static RuntimeAnimatorController BuildAnimator(Sprite[] sprites)
        {
            string path = Art + "/Citizen.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            string[] directions = { "Down", "Up", "Side" };
            for (int d=0;d<3;d++)
            for (int walk=0;walk<2;walk++)
            {
                string name = (walk==1?"Walk":"Idle") + directions[d];
                string clipPath = Art + "/" + name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip,clipPath); }
                clip.frameRate = 8;
                var keys = walk==1 ? Enumerable.Range(0,5).Select(i=>new ObjectReferenceKeyframe{time=i/8f,value=sprites[d*4+i%4]}).ToArray() :
                    new[] {new ObjectReferenceKeyframe{time=0,value=sprites[d*4]},new ObjectReferenceKeyframe{time=1,value=sprites[d*4]}};
                AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding {path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=true; AnimationUtility.SetAnimationClipSettings(clip,settings);
                var state = machine.AddState(name); state.motion=clip; if(name=="IdleDown")machine.defaultState=state;
                EditorUtility.SetDirty(clip);
            }
            EditorUtility.SetDirty(controller); return controller;
        }
        static void ConfigureTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false; importer.npotScale=TextureImporterNPOTScale.None; importer.SaveAndReimport();
        }
        static void BuildAtlas()
        {
            string path=Art+"/Characters.spriteatlas";
            var atlas=AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
            if(atlas==null) {atlas=new SpriteAtlas();AssetDatabase.CreateAsset(atlas,path);}
            var prior=atlas.GetPackables(); if(prior.Length>0)atlas.Remove(prior);
            atlas.Add(Enumerable.Range(0,8).Select(i=>(UnityEngine.Object)AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Citizen"+i+".png")).ToArray());
            atlas.SetTextureSettings(new SpriteAtlasTextureSettings{filterMode=FilterMode.Point,generateMipMaps=false,sRGB=true});
            atlas.SetPlatformSettings(new TextureImporterPlatformSettings{name="DefaultTexturePlatform",maxTextureSize=1024,textureCompression=TextureImporterCompression.Uncompressed});
            EditorUtility.SetDirty(atlas);
        }
        static void BuildAudio()
        {
            WriteWave("CityAmbience",16,(t)=>{
                double[] notes={130.8128,155.5635,195.9977,233.0819};
                int beat=(int)(t*2)%32; double root=notes[(beat/8)%4];
                double envelope=Math.Pow(1-(t*2%1),3);
                return .10*Math.Sin(2*Math.PI*root*t)+.045*Math.Sin(2*Math.PI*root*1.5*t)+.075*envelope*Math.Sin(2*Math.PI*root*(beat%4==0?4:3)*t);
            },true);
            WriteWave("Interface",.09,t=>.22*Math.Sin(2*Math.PI*780*t)*(1-t/.09),false);
            WriteWave("Success",.42,t=>.22*Math.Sin(2*Math.PI*(t<.14?523.25:t<.28?659.25:783.99)*t)*Math.Min(1,t*80)*(1-t/.42),false);
            WriteWave("Retry",.18,t=>.17*Math.Sin(2*Math.PI*(250-t*500)*t)*(1-t/.18),false);
        }
        static void WriteWave(string name,double duration,Func<double,double> sample,bool music)
        {
            string path=Art+"/"+name+".wav";int rate=22050,count=(int)(rate*duration);
            using(var w=new BinaryWriter(File.Open(path,FileMode.Create)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
                for(int i=0;i<count;i++) w.Write((short)(Math.Max(-1,Math.Min(1,sample(i/(double)rate)))*32767));
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;
            settings.compressionFormat=music?AudioCompressionFormat.Vorbis:AudioCompressionFormat.ADPCM;
            settings.loadType=music?AudioClipLoadType.CompressedInMemory:AudioClipLoadType.DecompressOnLoad;
            settings.quality=.6f;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();
        }

        [MenuItem("Tools/CyberCity/Build Windows player")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/CyberCity");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/CyberCity/CyberCity.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("CyberCity player build failed: "+report.summary.result);
            Debug.Log("CyberCity Windows player built successfully. Size: "+report.summary.totalSize+" bytes.");
        }
    }
}
#endif
