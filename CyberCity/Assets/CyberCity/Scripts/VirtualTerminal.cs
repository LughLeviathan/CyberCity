using System;
using System.Collections.Generic;

namespace CyberCity
{
    // A fixed in-memory filesystem. Commands never reach the OS or network.
    public sealed class VirtualTerminal
    {
        public string Directory { get; private set; } = "/home/citizen";
        public bool FlagRead { get; private set; }
        public bool Solved { get; private set; }
        public const string Flag = "CYBER{verify_first}";
        static readonly Dictionary<string, string> Files = new Dictionary<string, string>
        {
            { "/home/citizen/README.txt", "Welcome, citizen. List ALL files, including hidden entries. Investigate .archive and read access.txt. Then submit the flag.\nTry: ls -a" },
            { "/home/citizen/notes.txt", "A hidden name begins with a dot. Read a file with cat. These are fictional training files." },
            { "/home/citizen/.archive/access.txt", "Case 042 / source verified\nFlag: " + Flag },
            { "/home/citizen/logs/audit.txt", "09:00  Message received\n09:02  Sender verified via official channel\n09:05  Suspicious link reported" }
        };
        static readonly HashSet<string> Directories = new HashSet<string> { "/", "/home", "/home/citizen", "/home/citizen/.archive", "/home/citizen/logs" };

        string Resolve(string value)
        {
            string path = value.StartsWith("/") ? value : Directory + "/" + value;
            var parts = new List<string>();
            foreach (string p in path.Split('/'))
            {
                if (p == "" || p == ".") continue;
                if (p == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
                else parts.Add(p);
            }
            return "/" + string.Join("/", parts);
        }

        public string Execute(string command)
        {
            string[] parts = command.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "";
            string argument = parts.Length > 1 ? parts[1].Trim() : "";
            switch (parts[0])
            {
                case "help": return "pwd          Print current folder\nls [-a]      List files (-a includes hidden entries)\ncd <folder>  Change folder; .. goes up\ncat <file>   Read a fictional file\nsubmit <flag>  Submit after finding evidence\nclear        Clear the terminal\nNo shell, downloads or external commands are available.";
                case "pwd": return Directory;
                case "ls":
                    bool all = argument == "-a" || argument == "-la" || argument == "-al";
                    string folder = argument == "" || all ? Directory : Resolve(argument);
                    if (!Directories.Contains(folder)) return "ls: folder not found";
                    var names = new SortedSet<string>(); string prefix = folder == "/" ? "/" : folder + "/";
                    foreach (string p in Files.Keys) AddEntry(p, prefix, all, names, false);
                    foreach (string p in Directories) AddEntry(p, prefix, all, names, true);
                    return names.Count == 0 ? "(empty folder)" : string.Join("   ", names);
                case "cd":
                    string target = argument == "" || argument == "~" ? "/home/citizen" : Resolve(argument);
                    if (!Directories.Contains(target)) return "cd: folder not found";
                    Directory = target; return Directory;
                case "cat":
                    string file = Resolve(argument);
                    if (!Files.TryGetValue(file, out string content)) return "cat: file not found";
                    if (file == "/home/citizen/.archive/access.txt") FlagRead = true;
                    return content;
                case "submit":
                    if (!FlagRead) return "Read the evidence file first. A flag without an investigation is not enough.";
                    if (argument != Flag) return "Flag not recognized. Copy the full flag, including braces.";
                    Solved = true; return "FLAG VERIFIED. Investigation complete.";
                case "clear": return "";
                default: return "Unknown command. Type help for supported training commands.";
            }
        }

        static void AddEntry(string path, string prefix, bool all, SortedSet<string> entries, bool directory)
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal) || path.Length <= prefix.Length) return;
            string rest = path.Substring(prefix.Length);
            if (rest.Contains("/") || (!all && rest.StartsWith("."))) return;
            entries.Add(rest + (directory ? "/" : ""));
        }
    }
}
