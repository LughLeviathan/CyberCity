#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace CyberCity.Editor
{
    public static class CyberCityInteriorBuilder
    {
        const string Art = "Assets/CyberCity/Resources/Interiors/";
        static readonly string[] Names = { "Professor Byte", "Alex", "Maya", "Detective Byte", "SysAdmin Tux", "Red", "Blue" };
        static readonly string[] Titles = { "CYBER ACADEMY / STUDY", "INTERNET CAFE / ALEX'S STUDIO", "INTERNET CAFE / MESSAGE ANALYSIS", "INTERNET CAFE / DETECTIVE OFFICE", "LINUX LAB / SERVER ROOM", "WEB SECURITY LAB / RED'S LAB", "WEB SECURITY LAB / BLUE'S SOC" };
        static readonly string[] Accents = { "FFCE79", "AC91FF", "FF87B2", "E5B778", "83D9AC", "FF7194", "70D5F7" };
        static Color32 C(string hex) => GameUI.Hex(hex);

        [MenuItem("Tools/CyberCity/Build NPC interiors")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before building interiors.");
            Directory.CreateDirectory(Art); BuildArt(); AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles(Art, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter; settings.spritePivot = new Vector2(.5f, 0);
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
            var previous = SceneManager.GetActiveScene();
            for (int i = 0; i < Names.Length; i++)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("Interior / " + Names[i]); var room = root.AddComponent<InteriorRoom>();
                room.citizenName = Names[i]; room.title = Titles[i];
                Color accent = GameUI.Hex(Accents[i]);
                for (int x = 5; x < 27; x++)
                    for (int y = -15; y <= -4; y++)
                        Prop(root, "floor", x + .5f, y, false, -1000, (x + y) % 2 == 0 ? Color.white : new Color(.94f,.94f,1));
                for (int x = 5; x < 27; x++)
                {
                    Prop(root, "wall", x + .5f, -4, false, -900, Color.Lerp(Color.white, accent, .25f));
                    if (x < 14 || x > 17) Prop(root, "wall", x + .5f, -16, false, 1000, Color.Lerp(Color.white, accent, .15f), new Vector2(1,.35f));
                }
                Boundary(root, "North wall", new Vector2(16, -3.8f), new Vector2(22,.35f));
                Boundary(root, "South wall", new Vector2(16, -15.8f), new Vector2(22,.35f));
                Boundary(root, "West wall", new Vector2(5, -9.8f), new Vector2(.3f,12));
                Boundary(root, "East wall", new Vector2(27, -9.8f), new Vector2(.3f,12));
                Prop(root, "rug", 16, -12, false, -980, accent, new Vector2(4,3));
                Prop(root, "exit", 16, -15.6f, false, -950, accent, new Vector2(2,1));
                Prop(root, "window", 10, -3.4f, false, -850, accent, new Vector2(1.5f,1));
                Prop(root, "window", 22, -3.4f, false, -850, accent, new Vector2(1.5f,1));
                Prop(root, "plant", 6, -5.5f); Prop(root, "plant", 26, -5.5f);
                Prop(root, "plant", 6, -14); Prop(root, "plant", 26, -14);
                Prop(root, "desk", 16, -6.5f, true, null, accent);
                Label(root, Titles[i], new Vector2(16,-2.1f), 21, .65f, Color.Lerp(accent,Color.white,.5f), 4.2f);
                Label(root, "EXIT  /  E", new Vector2(16,-15.1f), 4, .5f, Color.Lerp(accent,Color.white,.5f), 3.2f);
                switch (i)
                {
                    case 0:
                        for (int x = 7; x <= 11; x += 2) Prop(root, "shelf", x, -6);
                        Prop(root, "board", 16, -3.8f, false, -800, accent);
                        Prop(root, "desk", 23, -9); Prop(root, "desk", 23, -12);
                        Prop(root, "sofa", 8, -12); break;
                    case 1:
                        Prop(root, "sofa", 9, -8, true, null, accent); Prop(root, "coffee", 9, -10);
                        Prop(root, "desk", 23, -7, true, null, accent); Prop(root, "shelf", 24, -12);
                        Prop(root, "rug", 9, -10.5f, false, -970, accent, new Vector2(3,2));
                        Prop(root, "board", 16,-3.8f,false,-800,accent); break;
                    case 2:
                        Prop(root, "board", 9,-4.1f,false,-800,accent); Prop(root,"board",23,-4.1f,false,-800,accent);
                        Prop(root, "desk", 9,-8,true,null,accent); Prop(root,"desk",23,-11,true,null,accent);
                        Prop(root, "shelf", 7,-12); Prop(root,"shelf",25,-8); break;
                    case 3:
                        Prop(root,"board",16,-3.8f,false,-800,accent,new Vector2(2,1));
                        Prop(root,"shelf",7,-7); Prop(root,"shelf",9,-7); Prop(root,"shelf",25,-7);
                        Prop(root,"coffee",9,-11); Prop(root,"sofa",23,-11,true,null,accent); break;
                    case 4:
                        foreach (int x in new[] { 7,10,22,25 })
                            foreach (int y in new[] { -7,-11 }) Prop(root,"server",x,y,true,null,accent);
                        Prop(root,"board",16,-3.8f,false,-800,accent); break;
                    case 5:
                        Prop(root,"desk",9,-8,true,null,accent); Prop(root,"desk",23,-8,true,null,accent);
                        Prop(root,"server",7,-12,true,null,accent); Prop(root,"server",25,-12,true,null,accent);
                        Prop(root,"console",22,-12,true,null,accent);
                        Label(root,"VIRUS SWEEP",new Vector2(22,-10),4,.5f,Color.Lerp(accent,Color.white,.5f),3.2f);
                        Prop(root,"board",16,-3.8f,false,-800,accent); break;
                    case 6:
                        foreach (int x in new[] { 8,12,20,24 }) Prop(root,"desk",x,-6,true,null,accent);
                        Prop(root,"server",7,-10,true,null,accent); Prop(root,"server",25,-10,true,null,accent);
                        Prop(root,"board",16,-3.8f,false,-800,accent,new Vector2(2,1));
                        Prop(root,"sofa",23,-13,true,null,accent); break;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, InteriorRoom.ScenePath(Names[i]))) throw new IOException("Could not save " + Names[i]);
                SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true);
            }
            string[] paths = Names.Select(InteriorRoom.ScenePath).ToArray();
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path))
                .Concat(paths.Select(p => new EditorBuildSettingsScene(p, true))).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("CyberCity PASS: 7 distinct NPC interior scenes built and enabled.");
        }

        static void Boundary(GameObject root, string name, Vector2 p, Vector2 size)
        {
            var go = new GameObject(name, typeof(BoxCollider2D)); go.transform.SetParent(root.transform);
            go.transform.position = p; go.GetComponent<BoxCollider2D>().size = size;
        }
        static void Prop(GameObject root, string id, float x, float y, bool solid = true, int? order = null, Color? tint = null, Vector2? scale = null)
        {
            var go = new GameObject(id, typeof(SpriteRenderer)); go.transform.SetParent(root.transform);
            go.transform.position = new Vector2(x,y);
            Vector2 dimensions = scale ?? Vector2.one; go.transform.localScale = new Vector3(dimensions.x, dimensions.y, 1);
            var sr = go.GetComponent<SpriteRenderer>(); sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + id + ".png");
            sr.sortingOrder = order ?? Mathf.RoundToInt(-y * 32); sr.color = tint ?? Color.white;
            if (solid)
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(sr.sprite.bounds.size.x * .82f, .42f); col.offset = new Vector2(0,.22f);
            }
        }
        static void Label(GameObject root, string value, Vector2 p, float width, float height, Color color, float size)
        {
            var go = new GameObject(value, typeof(TextMeshPro)); go.transform.SetParent(root.transform); go.transform.position = p;
            var t = go.GetComponent<TextMeshPro>(); t.text = value; t.color = color; t.fontSize = size;
            t.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(width,height); t.GetComponent<MeshRenderer>().sortingOrder = 1100;
        }

        static void BuildArt()
        {
            Paint("floor",32,32,(t,r)=> { r(0,0,32,32,"253347"); r(1,1,30,30,"2C3D52"); r(2,29,28,1,"3B4C61"); r(30,1,1,28,"1F2A3D"); r(5,7,3,1,"304159"); r(21,22,4,1,"304159"); });
            Paint("wall",32,64,(t,r)=> { r(0,0,32,64,"111C30"); r(1,7,30,49,"28384F"); r(2,10,28,43,"30435B"); r(2,49,28,3,"425772"); r(0,4,32,3,"83A5B8"); r(0,59,32,5,"526783"); });
            Paint("rug",32,32,(t,r)=> { r(0,0,32,32,"1B2536"); r(1,1,30,30,"547080"); r(2,2,28,28,"23354B"); r(4,4,24,24,"354B63"); r(5,25,22,1,"6D8796"); });
            Paint("exit",64,16,(t,r)=> { r(0,0,64,16,"111C30"); r(2,2,60,12,"4E6479"); r(4,4,56,8,"A0BAC2"); r(28,5,8,3,"F5E8B7"); r(30,3,4,7,"F5E8B7"); });
            Paint("window",64,40,(t,r)=> { r(0,0,64,40,"10192D"); r(2,2,60,36,"54718C"); r(4,4,56,32,"152C49"); for(int x=7;x<58;x+=8) { r(x,6,5,10+x%13,"325171"); r(x+1,9,1,2,"81C4D8"); } r(31,4,2,32,"91B7CA"); r(4,35,56,1,"B8D9DC"); });
            Paint("desk",80,48,(t,r)=> { r(4,0,72,6,"111A2B"); r(7,5,5,13,"334259"); r(67,5,5,13,"334259"); r(2,15,76,13,"1B283D"); r(2,22,76,8,"51687E"); r(4,28,72,2,"A3B7C1"); r(25,29,30,18,"101C31"); r(27,32,26,12,"5CA8B8"); r(29,39,12,2,"BFECE5"); r(29,35,20,1,"D4F4E9"); r(36,28,8,4,"253B53"); r(27,23,28,3,"9BAFBA"); r(63,28,6,7,"E0CEAA"); });
            Paint("server",40,72,(t,r)=> { r(2,0,36,4,"0C1726"); r(0,4,40,66,"132138"); r(2,7,36,61,"405870"); r(4,8,32,58,"1C2E45"); for(int y=12;y<64;y+=12) { r(6,y,28,9,"0F1C30"); r(8,y+5,18,2,"637C94"); r(29,y+2,3,3,"8FE3CA"); } r(2,67,36,3,"8CA6B4"); });
            Paint("shelf",48,64,(t,r)=> { r(0,0,48,64,"182237"); r(2,2,44,60,"967C68"); r(5,4,38,56,"344051"); for(int y=5;y<58;y+=18) { for(int x=7;x<40;x+=6) r(x,y+3,4,10+x%4,x%3==0?"73B5BB":x%3==1?"B98DA3":"DFBD87"); r(3,y,42,3,"BD9E7B"); } r(3,60,42,2,"E5C5A0"); });
            Paint("plant",32,48,(t,r)=> { r(9,0,15,3,"152332"); r(10,3,13,13,"9B7A69"); r(8,14,17,4,"DBAD8D"); r(15,17,3,21,"416B63"); r(5,24,10,12,"317D70"); r(8,28,10,12,"65B18E"); r(18,22,9,13,"328D79"); r(17,32,7,13,"90CBA0"); r(11,35,6,11,"B0DCB0"); });
            Paint("board",80,48,(t,r)=> { r(0,0,80,48,"101C2F"); r(2,2,76,44,"A9A79C"); r(4,4,72,40,"38495B"); for(int x=10;x<70;x+=22) { r(x,12+x%11,15,19,"DAD8BB"); r(x+2,22+x%11,10,2,"62768A"); r(x+2,18+x%11,8,1,"62768A"); r(x+6,30+x%11,3,3,"E48C98"); } r(18,8,40,2,"CE818F"); });
            Paint("sofa",80,40,(t,r)=> { r(4,0,72,5,"10192D"); r(0,6,80,31,"20344B"); r(4,10,72,26,"708A9C"); r(10,10,60,20,"4C667F"); r(12,25,56,10,"9AB4C1"); r(4,10,7,20,"B9C9C9"); r(69,10,7,20,"829DAF"); r(39,10,2,14,"2B415B"); });
            Paint("coffee",48,32,(t,r)=> { r(4,0,40,4,"0E1A2C"); r(6,4,4,12,"4F4B53"); r(38,4,4,12,"4F4B53"); r(1,13,46,17,"6D6264"); r(3,17,42,12,"C2AB8A"); r(7,20,15,7,"ECE3BC"); r(31,21,7,7,"FAE5C2"); r(33,25,3,2,"664D55"); });
            Paint("console",48,64,(t,r)=> { r(3,0,42,5,"0F192C"); r(5,4,38,54,"283C54"); r(3,55,42,8,"9CB1C0"); r(8,28,32,26,"101C32"); r(11,31,26,20,"528697"); r(15,36,18,10,"91D1B1"); r(1,21,46,7,"91A3B3"); r(8,24,8,2,"F0DCAE"); r(31,23,5,4,"EF9A9B"); r(15,8,18,6,"132238"); });
        }
        static void Paint(string id, int w, int h, Action<Texture2D,Action<int,int,int,int,string>> draw)
        {
            var t = new Texture2D(w,h,TextureFormat.RGBA32,false); t.SetPixels32(new Color32[w*h]);
            draw(t,(x,y,width,height,hex)=> { var color=C(hex); for(int j=y;j<y+height;j++) for(int i=x;i<x+width;i++) t.SetPixel(i,j,color); });
            t.Apply(); File.WriteAllBytes(Art+id+".png",t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
        }
    }
}
#endif
