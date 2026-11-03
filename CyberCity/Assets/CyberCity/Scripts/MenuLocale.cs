using System.Collections.Generic;

namespace CyberCity
{
    // Menu-only localization. Language names are proper names; gameplay copy is unchanged.
    public static class MenuLocale
    {
        public static readonly string[] Codes = { "id", "en", "jv", "su" };
        public static readonly string[] Names = { "Bahasa Indonesia", "English", "Basa Jawa", "Basa Sunda" };
        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            { "menu.eyebrow", new[] { "S I J A W   /   FIELD OPERATIONS", "S I J A W   /   FIELD OPERATIONS", "S I J A W   /   FIELD OPERATIONS", "S I J A W   /   FIELD OPERATIONS" } },
            { "menu.title", new[] { "CYBER\nCITY", "CYBER\nCITY", "CYBER\nCITY", "CYBER\nCITY" } },
            { "menu.tagline", new[] { "THINK BEFORE YOU CLICK", "THINK BEFORE YOU CLICK", "THINK BEFORE YOU CLICK", "THINK BEFORE YOU CLICK" } },
            { "menu.start", new[] { "START", "START", "WIWITI", "MIMITIAN" } },
            { "menu.settings", new[] { "SETTINGS", "SETTINGS", "SETELAN", "SETELAN" } },
            { "menu.quit", new[] { "QUIT", "QUIT", "METU", "KALUAR" } },
            { "menu.hint", new[] { "Mouse, atau W / S lalu Enter", "Mouse, or W / S then Enter", "Mouse, utawa W / S banjur Enter", "Mouse, atanapi W / S teras Enter" } },
            { "settings.title", new[] { "SETTINGS", "SETTINGS", "SETELAN", "SETELAN" } },
            { "settings.audio_visual", new[] { "AUDIO  &  VISUAL", "AUDIO  &  VISUAL", "SWARA  &  VISUAL", "SORA  &  VISUAL" } },
            { "settings.sfx", new[] { "SFX VOLUME", "SFX VOLUME", "VOLUME SWARA", "VOLUME SORA" } },
            { "settings.vfx", new[] { "VFX INTENSITY", "VFX INTENSITY", "INTENSITAS VFX", "INTENSITAS VFX" } },
            { "settings.language", new[] { "LANGUAGE  /  BAHASA", "LANGUAGE", "BASA", "BASA" } },
            { "settings.back", new[] { "BACK", "BACK", "BALI", "UIH" } },
            { "name.eyebrow", new[] { "PATROL IDENTITY", "PATROL IDENTITY", "IDENTITAS PATROLI", "IDENTITAS PATROLI" } },
            { "name.title", new[] { "Who's on patrol?", "Who's on patrol?", "Sapa sing patroli?", "Saha nu patroli?" } },
            { "name.body", new[] { "Mentor di kota ini akan memanggilmu dengan nama ini.", "Mentors in this city will call you by this name.", "Mentor ing kutha iki bakal nyebut kowe nganggo jeneng iki.", "Mentor di kota ieu bakal nyebut anjeun ku ngaran ieu." } },
            { "name.placeholder", new[] { "Nama pengguna", "Username", "Jeneng pangguna", "Ngaran pamaké" } },
            { "name.helper", new[] { "3 sampai 16 karakter. Huruf, angka, spasi, dan underscore.", "3 to 16 characters. Letters, numbers, spaces, and underscore.", "3 nganti 16 karakter. Aksara, angka, spasi, lan underscore.", "3 dugi ka 16 karakter. Hurup, angka, spasi, sareng underscore." } },
            { "name.error_length", new[] { "Gunakan 3 sampai 16 karakter untuk nama pengguna.", "Use 3–16 characters for your username.", "Gunakake 3 nganti 16 karakter kanggo jeneng pangguna.", "Anggo 3 dugi ka 16 karakter kanggo ngaran pamaké." } },
            { "name.error_characters", new[] { "Gunakan huruf, angka, spasi, atau underscore saja.", "Use letters, numbers, spaces or underscore only.", "Gunakake aksara, angka, spasi, utawa underscore wae.", "Anggo hurup, angka, spasi, atanapi underscore wungkul." } },
            { "name.counter", new[] { "{0}/16", "{0}/16", "{0}/16", "{0}/16" } },
            { "name.begin", new[] { "BEGIN PATROL", "BEGIN PATROL", "WIWITI PATROLI", "MIMITI PATROLI" } },
            { "name.back", new[] { "BACK", "BACK", "BALI", "UIH" } },
            { "loading.eyebrow", new[] { "WELCOME TO CYBERCITY", "WELCOME TO CYBERCITY", "SUGENG RAWUH ING CYBERCITY", "WILUJENG SUMPING DI CYBERCITY" } },
            { "loading.greeting", new[] { "Selamat datang, {player}.", "Welcome, {player}.", "Sugeng rawuh, {player}.", "Wilujeng sumping, {player}." } },
            { "loading.hint", new[] { "Tekan E untuk berbicara.  WASD untuk bergerak.", "Press E to talk.  WASD to move.", "Pencet E kanggo guneman.  WASD kanggo mlaku.", "Pencét E kanggo ngobrol.  WASD kanggo leumpang." } },
            { "key.enter", new[] { "ENTER", "ENTER", "ENTER", "ENTER" } },
            { "key.escape", new[] { "ESC", "ESC", "ESC", "ESC" } },
            { "icon.close", new[] { "X", "X", "X", "X" } },
            { "icon.arrow", new[] { ">", ">", ">", ">" } }
        };
        public static string Get(string key, string language)
        {
            int index = System.Array.IndexOf(Codes, language);
            return Table.TryGetValue(key, out var values) ? values[index < 0 ? 0 : index] : key;
        }
        public static string ValidationError(string reason, string language)
        {
            if (reason == Get("name.error_length", "en")) return Get("name.error_length", language);
            if (reason == Get("name.error_characters", "en")) return Get("name.error_characters", language);
            return reason;
        }
    }
}
