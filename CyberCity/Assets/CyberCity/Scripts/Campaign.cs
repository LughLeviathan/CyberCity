using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace CyberCity
{
    [Serializable] public class Question
    {
        public string prompt;
        public string[] options;
        public int answer;
        public string explanation;
    }

    [Serializable] public class Evidence
    {
        public string label, text, explanation;
        public bool suspicious;
    }

    [Serializable] public class Mission
    {
        public string id, title, npc, area, kind, briefing, objective, takeaway;
        public string sender, subject, address, document, hint;
        public Question[] questions;
        public Evidence[] evidence;
        public WebScenario web;
    }

    [Serializable] public class Campaign
    {
        public int version;
        public string title;
        public Mission[] missions;
    }

    public static class CampaignLoader
    {
        public static string Status { get; private set; }
        public static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        public static Campaign Parse(string json)
        {
            var data = JsonUtility.FromJson<Campaign>(json);
            if (data == null || data.version != 1 || data.missions == null || data.missions.Length == 0)
                throw new InvalidDataException("Campaign must contain version 1 and at least one mission.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < data.missions.Length; i++)
            {
                var m = data.missions[i];
                if (m == null || string.IsNullOrWhiteSpace(m.id) || !ids.Add(m.id) || string.IsNullOrEmpty(m.npc) || string.IsNullOrEmpty(m.kind))
                    throw new InvalidDataException("Mission metadata is missing.");
                if (Array.IndexOf(new[] { "Professor Byte", "Alex", "Maya", "Detective Byte", "SysAdmin Tux", "Red", "Blue" }, m.npc) < 0 ||
                    Array.IndexOf(new[] { "quiz", "evidence", "terminal", "idor", "xss", "sqli", "soc", "final", "graduation" }, m.kind) < 0)
                    throw new InvalidDataException("Unsupported mentor or simulation in " + m.id);
                if ((m.kind == "quiz" && (m.questions == null || m.questions.Length == 0)) || (m.questions != null && m.questions.Length > 8))
                    throw new InvalidDataException("Questions are missing or out of range in " + m.id);
                if (m.questions != null)
                    foreach (var q in m.questions)
                        if (q == null || q.options == null || q.options.Length < 2 || q.options.Length > 3 || q.answer < 0 || q.answer >= q.options.Length || string.IsNullOrEmpty(q.prompt))
                            throw new InvalidDataException("Invalid question in " + m.id);
                if (m.kind == "evidence" && (m.evidence == null || m.evidence.Length < 3 || m.evidence.Length > 4 || Array.Exists(m.evidence, e => e == null) || !Array.Exists(m.evidence, e => e.suspicious)))
                    throw new InvalidDataException("Evidence is missing in " + m.id);
                if (m.kind == "idor" || m.kind == "xss" || m.kind == "sqli")
                { if (m.web == null) throw new InvalidDataException("Missing browser scenario in " + m.id); m.web.Validate(m.kind); }
            }
            return data;
        }

        public static Campaign Load()
        {
            // A hash detects corruption. It is not a signature or proof of a pack's author.
            string local = Resources.Load<TextAsset>("campaign").text;
            string expected = Resources.Load<TextAsset>("campaign_hash").text.Trim();
            if (!string.Equals(Hash(local), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Bundled campaign checksum failed.");
            string path = Path.Combine(Application.persistentDataPath, "ScenarioPacks", "campaign.json");
            try
            {
                if (File.Exists(path))
                {
                    string candidate = File.ReadAllText(path);
                    string checksum = File.ReadAllText(path + ".sha256").Trim();
                    if (!string.Equals(Hash(candidate), checksum, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Optional pack checksum failed.");
                    Campaign pack = Parse(candidate);
                    Status = "LOCAL PACK / VERIFIED";
                    return pack;
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                Debug.LogWarning("CyberCity: optional pack rejected; using bundled content. " + e.Message);
            }
            Status = "OFFLINE PACK / VERIFIED";
            return Parse(local);
        }

        public static int UnlockForNpc(Campaign campaign, string npc) => Math.Max(0, Array.FindIndex(campaign.missions, m => m.npc == npc));
        public static int UnlockForArea(Campaign campaign, string area) => Math.Max(0, Array.FindIndex(campaign.missions, m => m.area == area));
    }
}
