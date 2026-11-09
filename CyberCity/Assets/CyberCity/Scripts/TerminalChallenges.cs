using System;

namespace CyberCity
{
    [Serializable] public class TerminalProgress
    {
        public string id;
        public bool solved;
        public int hints;
    }

    public sealed class TerminalChallenge
    {
        public readonly string id, title, objective, flag;
        public readonly string[] hints;
        public TerminalChallenge(string id, string title, string objective, string flag, params string[] hints)
        { this.id = id; this.title = title; this.objective = objective; this.flag = flag; this.hints = hints; }
    }

    public static class TerminalChallenges
    {
        public static readonly TerminalChallenge[] All = {
            new TerminalChallenge("hidden-file", "Hidden file", "Find the archived access evidence. A dot can hide a name.", "CYBER{verify_first}",
                "A normal listing leaves something out.", "Compare ls with ls -a, then investigate the folder that appears."),
            new TerminalChallenge("log-hunt", "Log hunt", "Find the source with the most failed logins. Submit CYBER{<source IP>}.", "CYBER{203.0.113.42}",
                "Successful sign-ins are noise. Count failed attempts per source.", "Locate auth.log with find. Use grep to isolate Failed lines; pipes can narrow the output."),
            new TerminalChallenge("permissions", "Least privilege", "Read the private evidence using only the permission its owner needs.", "CYBER{owner_read_only}",
                "Identify yourself and inspect the file's owner and permissions.", "The owner only needs read access. chmod 400 <file> grants that without write, execute or public access.")
        };
        public static TerminalProgress[] Fresh() => Array.ConvertAll(All, c => new TerminalProgress { id = c.id });
    }
}
