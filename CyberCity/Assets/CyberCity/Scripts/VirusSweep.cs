using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CyberCity
{
    public sealed class VirusSweep : MonoBehaviour
    {
        public VirusRun Run { get; private set; }
        GameUI ui;
        RectTransform field, hero, pause;
        Image heroImage;
        Text status, feedback;
        Sprite virusSprite;
        readonly Dictionary<int,RectTransform> sprites = new Dictionary<int,RectTransform>();
        readonly HashSet<int> live = new HashSet<int>();
        bool resultShown;
        bool Indonesian => ui.Language == "id";

        public void Initialize(GameUI owner)
        {
            ui = owner; Run = new VirusRun();
            field = ui.Box(transform,"Virus arena",0,0,VirusRun.Width,VirusRun.Height,GameUI.Ink);
            field.gameObject.AddComponent<RectMask2D>();
            for (int x = 0; x < VirusRun.Width; x += 32) ui.Box(field,"Grid",x,0,1,VirusRun.Height,new Color(.09f,.17f,.24f)).GetComponent<Image>().raycastTarget=false;
            for (int y = 0; y < VirusRun.Height; y += 32) ui.Box(field,"Grid",0,y,VirusRun.Width,1,new Color(.09f,.17f,.24f)).GetComponent<Image>().raycastTarget=false;
            virusSprite = Resources.Load<Sprite>("Interiors/virus");
            hero = ui.Rect(field,"Defender",0,0,40,58); hero.pivot = new Vector2(.5f,.5f);
            heroImage = hero.gameObject.AddComponent<Image>(); heroImage.sprite = ui.Game.player.GetComponent<SpriteRenderer>().sprite;
            heroImage.preserveAspect = true; heroImage.raycastTarget=false;
            status = ui.Text(transform,"",776,10,280,140,25,GameUI.Cyan,true);
            ui.Text(transform,Indonesian ? "WASD / panah\nGerak\n\nMouse + klik kiri\nBidik dan tembak\n\nSPACE\nTembak otomatis\n\nESC\nJeda" : "WASD / arrows\nMove\n\nMouse + left click\nAim and shoot\n\nSPACE\nAuto-aim fire\n\nESC\nPause",776,152,280,235,18,GameUI.Muted);
            ui.Button(transform,"II",962,384,94,44,TogglePause);
            feedback=ui.Text(transform,"",0,443,1056,38,18,GameUI.Gold);
            EventSystem.current?.SetSelectedGameObject(null); Render();
        }

        public void TogglePause()
        {
            if (Run == null || Run.Finished) return;
            Run.Paused = !Run.Paused;
            if (!Run.Paused) { Destroy(pause.gameObject); pause=null; EventSystem.current?.SetSelectedGameObject(null); return; }
            pause = ui.Box(field,"Arcade paused",0,0,VirusRun.Width,VirusRun.Height,new Color(.03f,.05f,.12f,.96f));
            ui.Text(pause,Indonesian ? "SIMULASI DIJEDA" : "SIMULATION PAUSED",100,98,600,54,32,GameUI.Cyan,true);
            var resume=ui.Button(pause,Indonesian ? "LANJUTKAN" : "RESUME",100,184,550,56,TogglePause,GameUI.Cyan);
            ui.Button(pause,Indonesian ? "Akhiri latihan / kembali ke Red" : "End practice / return to Red",100,258,550,56,()=>ui.CloseModal());
            EventSystem.current?.SetSelectedGameObject(resume.gameObject);
        }

        void Update()
        {
            if (Run == null || Run.Paused || Run.Finished) return;
            var k=Keyboard.current; var mouse=Mouse.current;
            Vector2 movement=Vector2.zero;
            if (k != null)
            {
                movement.x=(k.dKey.isPressed || k.rightArrowKey.isPressed ? 1:0)-(k.aKey.isPressed || k.leftArrowKey.isPressed ? 1:0);
                movement.y=(k.wKey.isPressed || k.upArrowKey.isPressed ? 1:0)-(k.sKey.isPressed || k.downArrowKey.isPressed ? 1:0);
            }
            bool keyboardFire=k != null && k.spaceKey.isPressed;
            Vector2 aim=Run.AutoAim(); bool mouseFire=false;
            if (mouse != null && !keyboardFire)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(field,mouse.position.ReadValue(),null,out Vector2 p);
                Vector2 target=new Vector2(p.x,VirusRun.Height+p.y); aim=target-Run.Player;
                mouseFire=mouse.leftButton.isPressed && field.rect.Contains(p);
            }
            int hp=Run.Health, kills=Run.Kills;
            Run.Step(Time.deltaTime,movement,aim,keyboardFire || mouseFire);
            if (Run.Health < hp) ui.Game.Play(ui.Game.errorSound);
            else if (Run.Kills > kills) ui.Game.Play(ui.Game.clickSound);
            Render();
            if (Run.Finished) Finish();
        }

        void Render()
        {
            hero.anchoredPosition=new Vector2(Run.Player.x,Run.Player.y-VirusRun.Height);
            heroImage.color=Run.Invulnerable ? new Color(1,.6f,.6f) : Color.white;
            live.Clear();
            foreach (var enemy in Run.Viruses) Draw(enemy.id,enemy.position,36,virusSprite,Color.white);
            foreach (var shot in Run.Shots) Draw(shot.id,shot.position,7,null,GameUI.Cyan);
            foreach (int id in sprites.Keys.Where(id=>!live.Contains(id)).ToArray()) { Destroy(sprites[id].gameObject); sprites.Remove(id); }
            status.text="HP  " + new string('+',Run.Health) + new string('-',3-Run.Health) + "\n\n" + (Indonesian ? "GELOMBANG " : "WAVE ") + Run.Wave + "/3\n" + Run.Kills + " / 24 VIRUS";
            feedback.text=Indonesian ? "Jangan diam di sudut. Bergerak sambil menembak; kontak virus mengurangi HP." : "Don't get cornered. Move while firing; virus contact costs HP.";
        }

        void Draw(int id,Vector2 p,float size,Sprite sprite,Color color)
        {
            live.Add(id);
            if (!sprites.TryGetValue(id,out var r))
            {
                r=ui.Rect(field,sprite == null ? "Pulse" : "Virus",0,0,size,size); r.pivot=new Vector2(.5f,.5f);
                var image=r.gameObject.AddComponent<Image>(); image.sprite=sprite; image.color=color; image.raycastTarget=false;
                sprites.Add(id,r);
            }
            r.anchoredPosition=new Vector2(p.x,p.y-VirusRun.Height);
        }

        void Finish()
        {
            if (resultShown) return; resultShown=true;
            var progress=ui.Game.Progress;
            progress.virusBestKills=Mathf.Max(progress.virusBestKills,Run.Kills);
            if (Run.Won) progress.virusCleared=true;
            ui.Game.Save(); ui.Game.Play(Run.Won ? ui.Game.successSound : ui.Game.errorSound);
            var overlay=ui.Box(field,"Arcade result",0,0,VirusRun.Width,VirusRun.Height,new Color(.03f,.05f,.12f,.97f));
            ui.Text(overlay,Run.Won ? "SYSTEM CLEAN" : "CONNECTION LOST",60,80,650,70,38,Run.Won ? GameUI.Teal : GameUI.Gold,true);
            ui.Text(overlay,Run.Won ? (Indonesian ? "24/24 virus dibersihkan. Kembali bicara dengan Red untuk melapor dan menerima lencana." : "24/24 viruses cleared. Talk to Red to report and receive your badge.") :
                (Indonesian ? "Virus dibersihkan: " : "Viruses cleared: ") + Run.Kills + "/24\n" + (Indonesian ? "XP campaign tetap utuh. Kamu bisa mencoba lagi." : "Campaign XP is unchanged. You can try again."),60,163,640,100,23);
            var b=ui.Button(overlay,Indonesian ? "KEMBALI KE RED" : "RETURN TO RED",60,292,410,58,ui.CloseModal,GameUI.Cyan);
            if (!Run.Won) ui.Button(overlay,Indonesian ? "ULANGI" : "RETRY",490,292,200,58,()=>ui.ShowVirusBriefing(ui.Game.Travel.Occupant));
            EventSystem.current?.SetSelectedGameObject(b.gameObject);
        }
    }
}
