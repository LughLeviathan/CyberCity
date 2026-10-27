using System.Collections;
using UnityEngine;

namespace CyberCity
{
    // Mission access still belongs to CampaignLoader and the existing NPC interaction path.
    public sealed class WorldArea : MonoBehaviour
    {
        public string areaName;
        public Transform door;
        public SpriteRenderer building, glow, lockIcon;
        public SpriteRenderer[] fragments;
        public bool IsOpen { get; private set; }
        public bool IsAnimating { get; private set; }
        public int UnlockAnimations { get; private set; }
        public int UnlockAfter => CampaignLoader.UnlockForArea(game.Campaign, areaName);
        CyberCityGame game;
        static readonly Color LockedTint = new Color(.38f, .42f, .55f, 1);

        public void Initialize(CyberCityGame owner)
        {
            if (game != null) game.ProgressChanged -= ProgressChanged;
            game = owner; game.ProgressChanged += ProgressChanged;
            StopAllCoroutines(); Apply(game.Progress.completed >= UnlockAfter);
        }
        void OnDestroy() { if (game != null) game.ProgressChanged -= ProgressChanged; }
        void ProgressChanged(int before, int after)
        {
            if (after == 0) UnlockAnimations = 0;
            bool open = after >= UnlockAfter;
            StopAllCoroutines();
            if (open && !IsOpen && after > before) { IsOpen = true; StartCoroutine(Unlock()); }
            else Apply(open);
        }
        void Apply(bool open)
        {
            IsOpen = open; IsAnimating = false;
            building.color = open ? Color.white : LockedTint;
            glow.enabled = open; glow.color = Color.white;
            lockIcon.enabled = !open;
            foreach (var f in fragments) f.enabled = false;
        }
        IEnumerator Unlock()
        {
            // Completion opens a debrief. Show the transition when the map is visible again.
            while (game.UI.HasModal) yield return null;
            UnlockAnimations++; IsAnimating = true;
            lockIcon.enabled = false; glow.enabled = true;
            game.UI.Toast(areaName + " unlocked");
            Vector3 origin = lockIcon.transform.localPosition;
            for (float t = 0; t < .85f; t += Time.unscaledDeltaTime)
            {
                float a = Mathf.Clamp01(t / .85f);
                building.color = Color.Lerp(LockedTint, Color.white, a);
                glow.color = new Color(1, 1, 1, a);
                for (int i = 0; i < fragments.Length; i++)
                {
                    var f = fragments[i]; float direction = i == 0 ? -1 : 1;
                    f.enabled = true;
                    f.transform.localPosition = origin + new Vector3(direction * (.125f + a * .55f), a * .3f - a * a * .65f, 0);
                    f.transform.localRotation = Quaternion.Euler(0, 0, direction * a * 75);
                    f.color = new Color(1, 1, 1, 1 - a);
                }
                yield return null;
            }
            Apply(true);
        }
    }
}
