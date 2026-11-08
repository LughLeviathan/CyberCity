#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
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
        internal const float CharacterPPU = 340;
        internal static readonly string[] CharacterSheets =
        {
            "main_hacker", "dr_crypto", "nova", "glitch", "firewall", "admin", "proxy", "cipher"
        };
        internal static string CharacterPath(int index) => Root + "/Character/" + CharacterSheets[index] + "_sheet_8x.png";
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
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            var materialPath = Art + "/PixelUnlit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, materialPath); }
            var world = CyberCityWorldBuilder.BuildWorld(mat);
            foreach (var sr in world.GetComponentsInChildren<SpriteRenderer>()) sr.sharedMaterial = mat;
            foreach (var tm in world.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapRenderer>()) tm.sharedMaterial = mat;
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(UniversalAdditionalCameraData));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(WorldLayout.Center.x, WorldLayout.Center.y, -10);
            var camera = cameraGo.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = WorldLayout.Height / 2;
            camera.rect = new Rect(0, 0, 1, 1); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = GameUI.Ink; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            camera.allowHDR = false; camera.allowMSAA = false;
            var pixel = cameraGo.AddComponent<PixelPerfectCamera>();
            pixel.assetsPPU = 32; pixel.refResolutionX = WorldLayout.CameraWidth; pixel.refResolutionY = WorldLayout.CameraHeight;
            pixel.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            pixel.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;
            // URP exposes this setting in its inspector, but has no public filter setter.
            var pixelSettings = new SerializedObject(pixel);
            pixelSettings.FindProperty("m_FilterMode").enumValueIndex = (int)PixelPerfectCamera.PixelPerfectFilterMode.Point;
            pixelSettings.ApplyModifiedPropertiesWithoutUndo();
            cameraGo.AddComponent<WorldCamera>();
            var playerGo = Actor("Citizen / Player", characters[0][0], WorldLayout.Spawn, mat);
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
            Vector2[] positions = { WorldLayout.FromPixel(498,185), WorldLayout.FromPixel(214,398), WorldLayout.FromPixel(175,409), WorldLayout.FromPixel(252,390), WorldLayout.FromPixel(745,242), WorldLayout.FromPixel(817,391), WorldLayout.FromPixel(860,413) };
            Color[] colors = { GameUI.White, GameUI.Violet, GameUI.Pink, GameUI.Gold, GameUI.Teal, GameUI.Pink, GameUI.Cyan };
            var campaign = CampaignLoader.Load();
            var citizens = new Citizen[7];
            for (int i = 0; i < 7; i++)
            {
                var go = Actor(names[i], characters[i + 1][0], positions[i], mat); go.transform.SetParent(citizenRoot.transform);
                var col = go.AddComponent<CircleCollider2D>(); col.radius = .23f; col.offset = new Vector2(0,.17f);
                var npc = go.AddComponent<Citizen>(); npc.citizenName = names[i]; npc.role = roles[i]; npc.area = areas[i];
                npc.accent = colors[i]; npc.unlockAfter = CampaignLoader.UnlockForNpc(campaign, names[i]); npc.idleFrames = new[] { characters[i + 1][0], characters[i + 1][2] };
                citizens[i] = npc;
            }
            AddBoundary("North boundary", new Vector2(WorldLayout.Width/2,.15f), new Vector2(WorldLayout.Width+1,.3f));
            AddBoundary("South boundary", new Vector2(WorldLayout.Width/2,-WorldLayout.Height-.15f), new Vector2(WorldLayout.Width+1,.3f));
            AddBoundary("West boundary", new Vector2(-.15f,-WorldLayout.Height/2), new Vector2(.3f,WorldLayout.Height+1));
            AddBoundary("East boundary", new Vector2(WorldLayout.Width+.15f,-WorldLayout.Height/2), new Vector2(.3f,WorldLayout.Height+1));
            var systems = new GameObject("CyberCity / Game systems");
            var ui = systems.AddComponent<GameUI>();
            ui.cityPreview = AssetDatabase.LoadAssetAtPath<Sprite>(CyberCityWorldBuilder.Textures + "world_preview.png");
            var game = systems.AddComponent<CyberCityGame>(); game.player = player; game.citizens = citizens; game.worldCamera = camera;
            game.areas = world.GetComponentsInChildren<WorldArea>();
            game.music = systems.AddComponent<AudioSource>(); game.music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "/CityAmbience.wav"); game.music.loop = true; game.music.playOnAwake = false;
            game.effects = systems.AddComponent<AudioSource>(); game.effects.playOnAwake = false;
            game.clickSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "/Interface.wav");
            game.successSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "/Success.wav");
            game.errorSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "/Retry.wav");
            PlayerSettings.companyName = "Sijaw"; PlayerSettings.productName = "CyberCity";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = false;
            QualitySettings.vSyncCount = 1;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save CyberCity scene; legacy character assets were retained.");
            RemoveLegacyDummies();
            var prior = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).Select(s => new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(prior).ToArray();
            AssetDatabase.SaveAssets();
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.in2DMode = true;
                SceneView.lastActiveSceneView.LookAt(new Vector3(WorldLayout.Center.x,WorldLayout.Center.y,0), Quaternion.identity, 18);
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
            string path = CharacterPath(index);
            if (!File.Exists(path)) throw new FileNotFoundException("Missing CyberCity character sheet: " + path, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != 1024 || height != 3072)
                throw new InvalidOperationException("Character sheet " + path + " must be 1024x3072 (4x8 cells of 256x384); found " + width + "x" + height + ".");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = CharacterPPU; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 4096;
            var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            // Apply settings and slicing together, then synchronously import once below.
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rectangles = new SpriteRect[32];
            for (int i=0; i<rectangles.Length; i++)
            {
                string name = CharacterSheets[index] + "_" + i.ToString("00");
                // Source rows run top to bottom; Unity sprite rects use a bottom-left origin.
                rectangles[i] = new SpriteRect { name=name, rect=new Rect(i%4*256,3072-(i/4+1)*384,256,384),
                    alignment=SpriteAlignment.Custom,pivot=new Vector2(.5f,.03f),spriteID=existing.TryGetValue(name,out var id)?id:GUID.Generate() };
            }
            provider.SetSpriteRects(rectangles);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rectangles.Select(r => new SpriteNameFileIdPair(r.name,r.spriteID)));
            provider.Apply();
            AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name, StringComparer.Ordinal).ToArray();
            if (sprites.Length != 32 || sprites.Any(s => s.rect.width != 256 || s.rect.height != 384 || s.pixelsPerUnit != CharacterPPU))
                throw new InvalidOperationException("Character sheet " + path + " did not import as 32 full-resolution 256x384 sprites.");
            return sprites;
        }

        static RuntimeAnimatorController BuildAnimator(Sprite[] sprites)
        {
            string path = Art + "/Citizen.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            string[] directions = { "Down", "Up", "Side" };
            int[] idleRows = { 0, 1, 3 }, walkRows = { 4, 5, 7 };
            for (int d=0;d<3;d++)
            for (int walk=0;walk<2;walk++)
            {
                string name = (walk==1?"Walk":"Idle") + directions[d];
                string clipPath = Art + "/" + name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip,clipPath); }
                float fps = walk == 1 ? 8 : 4;
                int row = walk == 1 ? walkRows[d] : idleRows[d];
                clip.frameRate = fps;
                // Unity holds the last sprite for one frame; four keys give a 4/fps loop.
                var keys = Enumerable.Range(0,4).Select(i=>new ObjectReferenceKeyframe{time=i/fps,value=sprites[row*4+i]}).ToArray();
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
        static void RemoveLegacyDummies()
        {
            // Called only after the replacement scene has been saved successfully.
            for (int i = 0; i < 8; i++) AssetDatabase.DeleteAsset(Art + "/Citizen" + i + ".png");
            AssetDatabase.DeleteAsset(Art + "/Characters.spriteatlas");
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
