using System.Linq;

namespace CyberCity
{
    public static class MissionRequirements
    {
        public static bool IsSatisfied(Mission mission, ProgressData progress)
        {
            switch (mission.kind)
            {
                case "terminal": return progress.terminal.Length == TerminalChallenges.All.Length && progress.terminal.All(t => t.solved);
                case "idor": case "xss": case "sqli": return Verified(progress, mission.id, mission.kind);
                case "soc": return progress.soc.Any(s => s.missionId == mission.id && s.complete);
                case "final": return mission.finalLabs.All(l => Verified(progress, mission.id, l.kind)) && progress.soc.Any(s => s.missionId == mission.id && s.complete);
                default: return true;
            }
        }
        static bool Verified(ProgressData progress, string id, string kind) => progress.web.Any(w => w.missionId == id && w.kind == kind && w.verified);
    }
}
