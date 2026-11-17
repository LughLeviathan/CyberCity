using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CyberCity
{
    [Serializable] public sealed class MentorLine
    {
        public string npc, greetingId, greetingEn, replyId, replyEn, lessonId, lessonEn;
    }
    [Serializable] public sealed class MentorConversations { public MentorLine[] mentors; }

    public sealed partial class GameUI
    {
        GameObject roomFade;
        MentorConversations conversations;
        public VirusSweep Arcade { get; private set; }
        string Choose(string id, string en) => Language == "id" ? id : en;

        public IEnumerator FadeRoom(bool toBlack, string title)
        {
            if (roomFade == null)
            {
                roomFade = Box(CanvasRoot, "Room transition", 0, 0, 1600, 900, Ink).gameObject;
                Stretch((RectTransform)roomFade.transform);
                var label = Text(roomFade.transform, title, 0, 0, 1300, 70, 28, White, true);
                Anchor(label.rectTransform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero);
                label.alignment = TextAnchor.MiddleCenter;
                roomFade.AddComponent<CanvasGroup>();
                EventSystem.current?.SetSelectedGameObject(null);
            }
            var fade = roomFade.GetComponent<CanvasGroup>();
            for (float t = 0; t < .28f; t += Time.unscaledDeltaTime)
            {
                float a = Mathf.SmoothStep(0,1,t/.28f); fade.alpha = toBlack ? a : 1-a;
                yield return null;
            }
            fade.alpha = toBlack ? 1 : 0;
            if (!toBlack) { Destroy(roomFade); roomFade = null; }
        }

        public void ShowRoomDirectory(WorldArea area)
        {
            var c = OpenModal(area.areaName, Choose("Pilih ruangan tujuan", "Choose a room"),
                Choose("NPC menunggu di dalam. Masuk, dekati mereka, lalu tekan E untuk berbicara.", "Mentors are indoors. Enter, walk up to them, then press E to talk."));
            var residents = Game.citizens.Where(n => n.area == area.areaName).ToArray();
            Button first = null;
            for (int i = 0; i < residents.Length; i++)
            {
                var npc = residents[i]; bool open = Game.Progress.completed >= npc.unlockAfter;
                var b = Button(c, (npc == Game.Target ? "!  " : "") + npc.citizenName + "   /   " + (open ? Choose("MASUK", "ENTER") : Choose("TERKUNCI", "LOCKED")),
                    0, i*110, 1056, 64, () => Game.Travel.Enter(npc), open ? npc.accent : (Color?)null);
                b.interactable = open; if (open && first == null) first = b;
                Text(c, npc.role + (open ? "" : Choose(" — selesaikan misi sebelumnya", " — finish earlier missions")), 16, i*110+68, 1000, 25, 17, Muted);
            }
            Button(c, Choose("Tetap di kota", "Stay in the city"), 0, 420, 360, 56, CloseModal);
            EventSystem.current?.SetSelectedGameObject(first?.gameObject);
        }

        RectTransform DialoguePanel(Citizen npc, string speaker, string line, bool playerSpeaking)
        {
            RemoveModal();
            modal = Box(CanvasRoot, "Dialogue overlay", 0, 0, 1600, 900, new Color(.02f,.03f,.08f,.25f)).gameObject;
            Stretch((RectTransform)modal.transform);
            var panel = Box(modal.transform, "Conversation", 0, 0, 1120, 310, Panel);
            Anchor(panel, new Vector2(.5f,0), new Vector2(.5f,0), new Vector2(0,60));
            Box(panel, "Pixel border", 0, 0, 1120, 4, npc.accent);
            var portrait = Box(panel, "Portrait", 20, 25, 128, 176, Raised);
            var sprite = Rect(portrait, "Character", 8, 6, 112, 160).gameObject.AddComponent<Image>();
            sprite.sprite = (playerSpeaking ? Game.player.GetComponent<SpriteRenderer>() : npc.GetComponent<SpriteRenderer>()).sprite;
            sprite.preserveAspect = true; sprite.raycastTarget = false;
            Text(panel, speaker, 176, 24, 910, 36, 25, playerSpeaking ? Cyan : npc.accent, true);
            Text(panel, line, 176, 72, 910, 112, 23);
            Text(panel, Game.Travel.Room.title, 20, 218, 138, 60, 11, Muted);
            Content = Rect(panel, "Dialogue choices", 176, 196, 916, 98);
            return Content;
        }

        public void ShowConversation(Citizen npc, int step = 0)
        {
            if (!Game.CanTalk(npc)) return;
            if (conversations == null) conversations = JsonUtility.FromJson<MentorConversations>(Resources.Load<TextAsset>("npc_dialogue").text);
            var entry = conversations.mentors.Single(m => m.npc == npc.citizenName);
            bool mc = step == 1 || step == 3;
            string line = step == 0 ? Choose(entry.greetingId, entry.greetingEn) : step == 1 ? Choose(entry.replyId, entry.replyEn) :
                step == 2 ? Choose(entry.lessonId, entry.lessonEn) : Choose("Apa yang bisa kubantu hari ini?", "What can I help with today?");
            var c = DialoguePanel(npc, mc ? Game.Progress.playerName + " / MC" : npc.citizenName, line, mc);
            Button first;
            if (step < 3)
                first = Button(c, Choose("LANJUT  >", "CONTINUE  >"), 0, 20, 330, 58, () => ShowConversation(npc, step+1), npc.accent);
            else
            {
                first = Button(c, Choose("Tugas utama", "Main assignment"), 0, 0, 294, 44, () => {
                    if (Game.Current == null) { ShowEnding(); return; }
                    if (Game.Target == npc) { ShowBriefing(Game.Current); return; }
                    ShowMessage(npc.citizenName, Choose("{player}, temui ", "{player}, visit ") + Game.Current.npc + " / " + Game.Current.area +
                        Choose(". Masuk ke ruangannya untuk melanjutkan: ", ". Enter their room to continue: ") + Game.Current.title, Choose("Baik, sampai nanti", "Got it. See you soon"));
                }, npc.accent);
                if (npc.citizenName == "Red")
                    Button(c, Game.Progress.virusCleared && !Game.Progress.virusReported ? Choose("Lapor Virus Sweep", "Report Virus Sweep") : "Virus Sweep", 310, 0, 294, 44, () => ShowVirusBriefing(npc));
                Button(c, Choose("Sampai nanti", "See you later"), 620, 0, 294, 44, CloseModal);
                Text(c, Choose("ESC: tutup dialog. Pintu selatan: kembali ke kota.", "ESC: close dialogue. South door: return to city."), 0, 64, 916, 28, 16, Muted);
            }
            EventSystem.current?.SetSelectedGameObject(first.gameObject);
        }

        public void ShowVirusBriefing(Citizen red)
        {
            if (!Game.CanTalk(red) || red.citizenName != "Red") return;
            if (Game.Progress.virusCleared && !Game.Progress.virusReported)
            {
                Game.Progress.virusReported = true; Game.Save(); Game.Play(Game.successSound);
                ShowMessage("Red / VIRUS SWEEP COMPLETE", Choose(
                    "{player}: Tiga gelombang berhasil kubersihkan.\n\nRed: Kerja bagus! Lencana VIRUS DEFENDER sekarang milikmu. Simulasi ini melatih refleks; di dunia nyata, tangani malware dengan isolasi, pembaruan, dan alat keamanan.\n\nKamu boleh mengulang latihan kapan saja.",
                    "{player}: All three waves are clear.\n\nRed: Great work! You earned the VIRUS DEFENDER badge. This is reflex training; real malware calls for isolation, updates and security tools.\n\nYou can replay the exercise any time."), Choose("Lencana diterima", "Badge received"));
                return;
            }
            var c = OpenModal("RED / SPECIAL QUEST", "VIRUS SWEEP", Choose("Opsional • 3 gelombang • 24 virus • Lencana VIRUS DEFENDER", "Optional • 3 waves • 24 viruses • VIRUS DEFENDER badge"));
            Text(c, Choose("Red: Lingkungan simulasi lab dipenuhi virus. Bantu aku membersihkannya!\n\n{player}: Aku akan menjaga jarak, membidik, lalu menembak. Setelah selesai, aku akan melapor padamu.",
                "Red: The lab simulation is full of viruses. Help me clear them out!\n\n{player}: I'll keep my distance, aim and fire. Then I'll report back to you."), 0, 8, 1030, 150, 24);
            var instructions = Box(c, "Arcade controls", 0, 178, 1056, 144, Raised);
            Text(instructions, Choose("WASD / panah: bergerak     Mouse: bidik     Klik kiri: tembak\nSPACE: tembak virus terdekat (tanpa mouse)     ESC: jeda\n3 HP. Hindari kontak. Gagal? Ulangi tanpa kehilangan XP campaign.",
                "WASD / arrows: move     Mouse: aim     Left click: fire\nSPACE: target nearest virus (keyboard only)     ESC: pause\n3 HP. Avoid contact. Retry without losing campaign XP."), 22, 16, 1010, 116, 21);
            Text(c, Choose("Setelah menang, tekan E pada Red dan laporkan quest untuk menerima lencana.", "After winning, talk to Red and report the quest to receive your badge."),0,340,1030,55,19,Gold);
            var start = Button(c, Choose("MULAI VIRUS SWEEP", "START VIRUS SWEEP"), 0, 416, 460, 58, () => {
                if (!Game.CanTalk(red)) return;
                Game.Progress.virusAccepted = true; Game.Save();
                var arena = OpenModal("SPECIAL QUEST / RED'S LAB", "VIRUS SWEEP", Choose("Jaga jarak. Bersihkan 3 gelombang. ESC: jeda.", "Keep moving. Clear 3 waves. ESC: pause."));
                Arcade = arena.gameObject.AddComponent<VirusSweep>(); Arcade.Initialize(this);
            }, Cyan);
            Button(c, Choose("Nanti saja", "Maybe later"),480,416,320,58,CloseModal);
            EventSystem.current?.SetSelectedGameObject(start.gameObject);
        }
    }
}
