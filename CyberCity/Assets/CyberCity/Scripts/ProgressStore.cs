using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CyberCity
{
    [Serializable] public class ProgressData
    {
        public int version = 2;
        public int completed;
        public int score;
        public int[] attempts = Array.Empty<int>();
        public string[] missionIds = Array.Empty<string>();
        public bool started;
        public float x = 14, y = -8.2f;
    }

    public static class ProgressStore
    {
        public const int MaxReward = 150, RetryPenalty = 15, MinReward = 75;
        public static int MaximumScore(Campaign campaign) => campaign.missions.Length * MaxReward;
        public static int Reward(int attempts, int hints = 0) => Mathf.Max(MinReward, MaxReward - attempts * RetryPenalty - hints * 5);
        public static string SavePath => Path.Combine(Application.persistentDataPath, "cybercity-progress.json");
        public static string LastError { get; private set; }

        public static ProgressData Create(Campaign campaign) => new ProgressData {
            attempts = new int[campaign.missions.Length], missionIds = campaign.missions.Select(m => m.id).ToArray()
        };

        public static ProgressData Load(Campaign campaign, string path = null)
        {
            LastError = null;
            string resolved = path ?? SavePath;
            foreach (string file in new[] { resolved, resolved + ".bak" })
            {
                if (!File.Exists(file)) continue;
                try
                {
                    var data = JsonUtility.FromJson<ProgressData>(File.ReadAllText(file));
                    if (data != null && data.version == 1)
                    {
                        LastError = "Your version 1 save belongs to the earlier course. A fresh patrol is ready for the revised challenges.";
                        return Create(campaign);
                    }
                    int count = campaign.missions.Length;
                    if (data == null || data.version != 2 || data.completed < 0 || data.completed > count ||
                        data.score < 0 || data.score > MaximumScore(campaign) || data.attempts == null || data.attempts.Length != count ||
                        data.missionIds == null || !data.missionIds.SequenceEqual(campaign.missions.Select(m => m.id)) ||
                        float.IsNaN(data.x) || float.IsNaN(data.y) || float.IsInfinity(data.x) || float.IsInfinity(data.y))
                    throw new InvalidDataException("Invalid save or different course route.");
                    if (Array.Exists(data.attempts, value => value < 0 || value > 100000))
                        throw new InvalidDataException("Invalid retry counts.");
                    data.x = Mathf.Clamp(data.x, 0.5f, 27.5f);
                    data.y = Mathf.Clamp(data.y, -21.5f, -0.5f);
                    return data;
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException)
                { LastError = "Save could not be read or belongs to a different course. A backup or fresh patrol was loaded."; }
            }
            return Create(campaign);
        }

        public static bool Save(ProgressData data, string path = null)
        {
            try
            {
                string resolved = path ?? SavePath;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resolved)));
                string temporary = resolved + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(resolved)) File.Replace(temporary, resolved, resolved + ".bak");
                else File.Move(temporary, resolved);
                LastError = null;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { LastError = "Progress could not be saved. Check free space and folder access."; Debug.LogWarning(LastError); return false; }
        }
    }
}
