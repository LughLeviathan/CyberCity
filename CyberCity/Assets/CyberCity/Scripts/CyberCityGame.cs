using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CyberCity
{
    [RequireComponent(typeof(GameUI))]
    public sealed class CyberCityGame : MonoBehaviour
    {
        public PlayerController player;
        public Citizen[] citizens;
        public Camera worldCamera;
        public AudioSource music, effects;
        public AudioClip clickSound, successSound, errorSound;
        public Campaign Campaign { get; private set; }
        public ProgressData Progress { get; private set; }
        public GameUI UI { get; private set; }
        public bool Started { get; private set; }
        public Mission Current => Progress.completed < Campaign.missions.Length ? Campaign.missions[Progress.completed] : null;
        public Citizen Target => Current == null ? null : citizens.FirstOrDefault(n => n.citizenName == Current.npc);
        public Citizen Nearest { get; private set; }
        float saveTimer;

        void OnEnable()
        {
            // Unity restores serializable gameplay data during an editor script reload,
            // but the UI's dictionaries and callback helpers must be reconstructed.
            if (Application.isPlaying && Campaign != null && Progress != null)
                StartCoroutine(RestoreInterface());
        }

        System.Collections.IEnumerator RestoreInterface()
        {
            yield return null;
            UI = GetComponent<GameUI>(); UI.Initialize(this);
        }

        void Start()
        {
            UI = GetComponent<GameUI>();
            try { Campaign = CampaignLoader.Load(); }
            catch (Exception e) { Debug.LogError("CyberCity content error: " + e.Message); UI.ShowFatal(e.Message); enabled = false; return; }
            Progress = ProgressStore.Load();
            if (Physics2D.OverlapCircle(new Vector2(Progress.x, Progress.y + .15f), .22f) != null)
            { Progress.x = 14; Progress.y = -8.2f; }
            player.transform.position = new Vector3(Progress.x, Progress.y, 0);
            music.volume = PlayerPrefs.GetFloat("CyberCityMusic", .32f);
            effects.volume = PlayerPrefs.GetFloat("CyberCityEffects", .7f);
            UI.Initialize(this);
        }

        public void Begin()
        {
            Started = true; Progress.started = true; UI.CloseModal(); UI.RefreshHud(); Save();
            if (!music.isPlaying) music.Play();
            UI.Toast(ProgressStore.LastError ?? "Welcome to CyberCity. Follow the gold mission marker.");
        }

        void Update()
        {
            if (Campaign == null) return;
            Nearest = citizens.OrderBy(n => Vector2.Distance(player.transform.position, n.transform.position)).FirstOrDefault();
            if (Nearest != null && Vector2.Distance(player.transform.position, Nearest.transform.position) > 1.65f) Nearest = null;
            player.CanMove = Started && !UI.HasModal;
            UI.UpdateWorldLabels();
            var k = Keyboard.current;
            if (Started && k != null)
            {
                if (k.escapeKey.wasPressedThisFrame) { if (UI.HasModal) UI.CloseModal(); else UI.ShowPause(); }
                else if (!UI.HasModal && k.eKey.wasPressedThisFrame && Nearest != null) Interact(Nearest);
                else if (!UI.HasModal && k.jKey.wasPressedThisFrame) UI.ShowJournal();
                else if (!UI.HasModal && k.hKey.wasPressedThisFrame) UI.ShowHelp();
                else if (!UI.HasModal && k.mKey.wasPressedThisFrame) UI.ShowMap();
            }
            if (Started && (saveTimer += Time.unscaledDeltaTime) > 15) { saveTimer = 0; Save(); }
        }

        public void Interact(Citizen npc)
        {
            if (!Started || UI.HasModal) return;
            Play(clickSound);
            if (Progress.completed < npc.unlockAfter)
            { UI.ShowMessage(npc.citizenName, "This lab is still locked. Complete your current mission to earn access.", "Return to the city"); return; }
            if (Current == null) { UI.ShowEnding(); return; }
            if (npc.citizenName != Current.npc)
            { UI.ShowMessage(npc.citizenName + "  /  " + npc.role, "Your next mission is \"" + Current.title + "\". Find " + Current.npc + " at " + Current.area + ". Follow the gold marker on the map.", "Keep exploring"); return; }
            UI.ShowBriefing(Current);
        }

        public void WrongAnswer()
        {
            Progress.attempts[Progress.completed]++;
            Save(); Play(errorSound);
        }

        public void CompleteMission()
        {
            if (Current == null) return;
            Mission finished = Current;
            int reward = Mathf.Max(75, 150 - Progress.attempts[Progress.completed] * 15);
            Progress.score += reward; Progress.completed++;
            Save(); Play(successSound); UI.RefreshHud(); UI.ShowDebrief(finished, reward);
        }

        public void Save()
        {
            if (Progress == null || !Started) return;
            Progress.x = player.transform.position.x; Progress.y = player.transform.position.y;
            if (!ProgressStore.Save(Progress)) UI.Toast(ProgressStore.LastError);
        }

        public void NewPatrol()
        {
            Progress = new ProgressData(); player.GetComponent<Rigidbody2D>().position = new Vector2(14, -8.2f);
            Begin();
        }

        public void Play(AudioClip clip) { if (clip != null && effects != null) effects.PlayOneShot(clip); }
        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() { Save(); }
    }
}
