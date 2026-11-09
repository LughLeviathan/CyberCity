using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CyberCity
{
    // In-memory training commands only. Never invokes a shell, file API, network or executable.
    public sealed class VirtualTerminal
    {
        sealed class TrainingFile
        {
            public string text;
            public int mode = 256; // octal 400: owner read only
            public TrainingFile(string text, int mode = 256) { this.text = text; this.mode = mode; }
        }
        readonly Dictionary<string, TrainingFile> files = new Dictionary<string, TrainingFile>();
        readonly HashSet<string> directories = new HashSet<string> { "/", "/home", "/home/citizen" };
        readonly TerminalProgress progress;
        public TerminalChallenge Challenge { get; }
        public string Directory { get; private set; } = "/home/citizen";
        public bool FlagRead { get; private set; }
        public bool Solved => progress.solved;
        public const string Flag = "CYBER{verify_first}";
        const string Home = "/home/citizen";

        public VirtualTerminal() : this("hidden-file") { }
        public VirtualTerminal(string challengeId, TerminalProgress saved = null)
        {
            Challenge = TerminalChallenges.All.FirstOrDefault(c => c.id == challengeId)
                ?? throw new ArgumentException("Unknown training challenge.");
            progress = saved ?? new TerminalProgress { id = challengeId };
            if (challengeId == "hidden-file")
            {
                Add("README.txt", "The archive contains the access evidence. Hidden names start with a dot. Explore deliberately; help describes available commands.");
                Add("notes.txt", "A filename beginning with a dot is hidden from a normal listing. All files here are fictional.");
                Add(".archive/access.txt", "Case 042 / source verified\nFlag: " + Challenge.flag);
            }
            else if (challengeId == "log-hunt")
            {
                Add("README.txt", "Investigate the fictional authentication log. Count FAILED sign-ins by source. Submit CYBER{<most frequent failed source IP>}.");
                Add("logs/auth.log", "09:00 Failed password src=203.0.113.42 user=admin\n09:01 Accepted password src=10.0.0.8 user=maya\n09:02 Failed password src=203.0.113.77 user=alex\n09:03 Failed password src=203.0.113.42 user=admin\n09:04 Failed password src=203.0.113.42 user=guest");
                Add("logs/service.log", "09:00 Service healthy\n09:05 Backup complete");
            }
            else
            {
                Add("README.txt", "The private evidence belongs to trainee, but read access was removed. Restore only the owner's read permission. Public, write and execute access are unnecessary.");
                Add("private/flag.txt", "Permission audit complete.\nFlag: " + Challenge.flag, 0);
            }
        }
        void Add(string relative, string text, int mode = 256)
        {
            string path = Home + "/" + relative; files[path] = new TrainingFile(text, mode);
            int slash = path.LastIndexOf('/');
            while (slash > 0) { directories.Add(path.Substring(0, slash)); slash = path.LastIndexOf('/', slash - 1); }
        }
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
        static List<string> Tokens(string command)
        {
            var result = new List<string>(); var token = new StringBuilder(); char quote = '\0';
            foreach (char c in command.Trim())
            {
                if ((c == '\'' || c == '"') && (quote == '\0' || quote == c)) { quote = quote == '\0' ? c : '\0'; continue; }
                if (char.IsWhiteSpace(c) && quote == '\0') { if (token.Length > 0) { result.Add(token.ToString()); token.Clear(); } }
                else token.Append(c);
            }
            if (token.Length > 0) result.Add(token.ToString());
            return result;
        }
        public string Execute(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return "";
            if (command.Length > 1024) return "Command too long. Try a shorter training command.";
            var stages = command.Split('|');
            if (stages.Length > 4 || stages.Any(string.IsNullOrWhiteSpace)) return "Pipe: use up to four non-empty commands separated by |.";
            string input = null;
            for (int i = 0; i < stages.Length; i++)
            {
                var tokens = Tokens(stages[i]);
                if (tokens.Count == 0) return "Type help for supported commands.";
                string cmd = tokens[0]; var args = tokens.Skip(1).ToArray();
                if (stages.Length > 1 && !new[] { "cat", "grep", "head", "tail", "wc", "find", "ls" }.Contains(cmd))
                    return "Pipe: only inspection commands can be piped. Run navigation, chmod or submit separately.";
                input = Run(cmd, args, input);
                if (input.StartsWith("ERROR:", StringComparison.Ordinal)) return input;
            }
            return input;
        }
        string Read(string path)
        {
            if (!files.TryGetValue(Resolve(path), out var file)) return "ERROR: file not found. Use ls or find to inspect filenames.";
            if ((file.mode & 256) == 0) return "ERROR: Permission denied. Inspect ownership with whoami and ls -l.";
            if (file.text.Contains(Challenge.flag) || (Challenge.id == "log-hunt" && Resolve(path).EndsWith("/auth.log", StringComparison.Ordinal))) FlagRead = true;
            return file.text;
        }
        string Run(string cmd, string[] args, string input)
        {
            string arg = args.Length == 0 ? "" : args[0];
            switch (cmd)
            {
                case "help": return "pwd; ls [-a] [-l] [folder]; cd <folder>; cat <file>\ngrep [-i] <text> [file]; find [folder] [-name pattern]\nhead/tail [-n count] [file]; wc [-l] [file]\nwhoami; chmod <400|u+r|u-r> <file>; submit <flag>; clear\nUse | to pass text to grep, head, tail or wc. No external commands exist.";
                case "pwd": return Directory;
                case "whoami": return "trainee";
                case "cd":
                    string target = arg == "" || arg == "~" ? Home : Resolve(arg);
                    if (!directories.Contains(target)) return "ERROR: cd: folder not found.";
                    Directory = target; return Directory;
                case "ls":
                    bool all = args.Any(a => a.StartsWith("-") && a.Contains("a"));
                    bool detailed = args.Any(a => a.StartsWith("-") && a.Contains("l"));
                    string folder = args.FirstOrDefault(a => !a.StartsWith("-")); folder = folder == null ? Directory : Resolve(folder);
                    if (!directories.Contains(folder)) return "ERROR: ls: folder not found.";
                    string prefix = folder == "/" ? "/" : folder + "/";
                    return string.Join(detailed ? "\n" : "   ", directories.Concat(files.Keys).Distinct().OrderBy(p => p, StringComparer.Ordinal)
                        .Where(p => p.StartsWith(prefix, StringComparison.Ordinal) && p.Length > prefix.Length && !p.Substring(prefix.Length).Contains("/") && (all || !p.Substring(prefix.Length).StartsWith(".")))
                        .Select(p => (detailed ? (directories.Contains(p) ? "dr-x------" : Mode(files[p].mode)) + " trainee  " : "") + p.Substring(prefix.Length) + (directories.Contains(p) ? "/" : "")));
                case "cat": return args.Length != 1 ? "ERROR: usage: cat <file>" : Read(arg);
                case "find":
                    string root = arg == "" || arg == "-name" ? Directory : Resolve(arg);
                    if (!directories.Contains(root)) return "ERROR: find: folder not found.";
                    int nameIndex = Array.IndexOf(args, "-name");
                    if (nameIndex >= 0 && nameIndex + 1 >= args.Length) return "ERROR: usage: find [folder] -name <pattern>";
                    string pattern = nameIndex >= 0 ? args[nameIndex + 1] : "*";
                    return string.Join("\n", files.Keys.Where(p => p.StartsWith(root == "/" ? "/" : root + "/", StringComparison.Ordinal)
                        && Regex.IsMatch(p.Substring(p.LastIndexOf('/') + 1), "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$")));
                case "grep":
                    bool ignore = args.Contains("-i"); var values = args.Where(a => a != "-i").ToArray();
                    if (values.Length == 0 || (input == null && values.Length < 2)) return "ERROR: usage: grep [-i] <text> <file>, or ... | grep <text>";
                    string source = input ?? Read(values[1]);
                    if (source.StartsWith("ERROR:")) return source;
                    return string.Join("\n", source.Split('\n').Where(l => l.IndexOf(values[0], ignore ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) >= 0));
                case "head": case "tail": case "wc":
                    int count = 10; int index = 0;
                    if (cmd != "wc" && arg == "-n")
                    { if (args.Length < 2 || !int.TryParse(args[1], out count) || count < 1 || count > 1000) return "ERROR: -n needs a number from 1 to 1000."; index = 2; }
                    else if (cmd == "wc" && arg == "-l") index = 1;
                    string body = input ?? (args.Length > index ? Read(args[index]) : "ERROR: provide a file or pipe text into " + cmd + ".");
                    if (body.StartsWith("ERROR:")) return body;
                    string[] lines = body.Length == 0 ? Array.Empty<string>() : body.Split('\n');
                    return cmd == "wc" ? lines.Length.ToString() : string.Join("\n", cmd == "head" ? lines.Take(count) : lines.Skip(Math.Max(0, lines.Length - count)));
                case "chmod":
                    if (args.Length != 2) return "ERROR: usage: chmod <400|u+r|u-r> <file>";
                    if (!files.TryGetValue(Resolve(args[1]), out var changed)) return "ERROR: chmod: file not found.";
                    if (arg == "u+r") changed.mode |= 256;
                    else if (arg == "u-r") changed.mode &= ~256;
                    else if (arg.Length == 3 && arg.All(c => c >= '0' && c <= '7')) changed.mode = Convert.ToInt32(arg, 8);
                    else return "ERROR: chmod: use a three-digit octal mode or u+r / u-r.";
                    return "Permissions updated: " + Mode(changed.mode) + " trainee";
                case "submit":
                    if (!FlagRead) return "Read the evidence file first. Investigate before submitting.";
                    if (arg != Challenge.flag) return "Flag not recognized. Include the complete CYBER{...} value.";
                    if (Challenge.id == "permissions" && files[Home + "/private/flag.txt"].mode != 256) return "Least privilege: grant only owner read access. Remove extra write, execute, group and public permissions.";
                    progress.solved = true; return "FLAG VERIFIED. Investigation complete.";
                case "clear": return "";
                default: return "Unknown command. Type help for supported training commands.";
            }
        }
        static string Mode(int mode)
        {
            var value = new StringBuilder("-");
            for (int bit = 8; bit >= 0; bit--) value.Append((mode & (1 << bit)) != 0 ? "rwx"[(8 - bit) % 3] : '-');
            return value.ToString();
        }
    }
}
