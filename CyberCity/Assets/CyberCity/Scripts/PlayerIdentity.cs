using System;

namespace CyberCity
{
    public static class PlayerIdentity
    {
        public static bool TryValidate(string value, out string name, out string error)
        {
            name = (value ?? "").Trim(); error = "";
            if (name.Length < 3 || name.Length > 16)
            { error = "Use 3–16 characters for your username."; return false; }
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '_')
                { error = "Use letters, numbers, spaces or underscore only."; return false; }
            return true;
        }

        public static string Personalize(string text, string name) => (text ?? "").Replace("{player}",
            TryValidate(name, out string clean, out _) ? clean : "Defender");
    }
}
