using System;
using System.IO;
using UnityEngine;

namespace CyberCity
{
    [Serializable] public class ProgressData
    {
        public int version = 1;
        public int completed;
        public int score;
        public int[] attempts = new int[12];
        public bool started;
        public float x = 14, y = -8.2f;
    }

    public static class ProgressStore
    {
        public static string SavePath => Path.Combine(Application.persistentDataPath, "cybercity-progress.json");
        public static string LastError { get; private set; }

        public static ProgressData Load(string path = null)
        {
            LastError = null;
            string resolved = path ?? SavePath;
            foreach (string file in new[] { resolved, resolved + ".bak" })
            {
                if (!File.Exists(file)) continue;
                try
                {
                    var data = JsonUtility.FromJson<ProgressData>(File.ReadAllText(file));
                    if (data == null || data.version != 1 || data.completed < 0 || data.completed > 12 ||
                        data.score < 0 || data.score > 1800 || data.attempts == null || data.attempts.Length != 12 ||
                        float.IsNaN(data.x) || float.IsNaN(data.y) || float.IsInfinity(data.x) || float.IsInfinity(data.y))
                        throw new InvalidDataException("Invalid save data.");
                    if (Array.Exists(data.attempts, value => value < 0 || value > 100000))
                        throw new InvalidDataException("Invalid retry counts.");
                    data.x = Mathf.Clamp(data.x, 0.5f, 27.5f);
                    data.y = Mathf.Clamp(data.y, -21.5f, -0.5f);
                    return data;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
                { LastError = "Save could not be read. A backup or fresh patrol was loaded."; }
            }
            return new ProgressData();
        }

        public static bool Save(ProgressData data, string path = null)
        {
            try
            {
                string resolved = path ?? SavePath;
                Directory.CreateDirectory(Path.GetDirectoryName(resolved));
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
