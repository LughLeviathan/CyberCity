using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CyberCity
{
    [Serializable] public class SocAlert { public string id, title, context; public string[] packetIds; public bool truePositive; }
    [Serializable] public class PacketLog { public string id, time, src, dst, protocol, info; }
    [Serializable] public class SocAttack { public string id, kind; public string[] packetIds; }
    [Serializable] public class IncidentReport { public string who = "", what = "", when = "", impact = "", mitigation = ""; }
    [Serializable] public class SocScenario
    {
        public string title, attackerIp, firstSeen;
        public SocAlert[] alerts;
        public PacketLog[] packets;
        public SocAttack[] attacks;
        public string[] requiredResponses, impactTerms;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(title) || !TrainingIp(attackerIp) || string.IsNullOrWhiteSpace(firstSeen) ||
                alerts == null || alerts.Length == 0 || alerts.Length > 8 || alerts.Any(a => a == null || string.IsNullOrEmpty(a.id) || a.packetIds == null || a.packetIds.Length == 0) ||
                packets == null || packets.Length == 0 || packets.Length > 256 || packets.Any(p => p == null || string.IsNullOrEmpty(p.id) || !TrainingIp(p.src) || !TrainingIp(p.dst) || string.IsNullOrEmpty(p.time) || string.IsNullOrEmpty(p.info)) ||
                attacks == null || attacks.Length == 0 || attacks.Any(a => a == null || string.IsNullOrEmpty(a.id) || Array.IndexOf(new[] { "IDOR", "XSS", "SQLi" }, a.kind) < 0 || a.packetIds == null || a.packetIds.Length == 0) ||
                requiredResponses == null || requiredResponses.Length == 0 || requiredResponses.Any(r => !new[] { "block-ip", "patch", "reset-credentials" }.Contains(r)) ||
                impactTerms == null || impactTerms.Length == 0 || impactTerms.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("Invalid fictional SOC scenario.");
            var ids = packets.Select(p => p.id).ToArray();
            if (ids.Distinct().Count() != ids.Length || alerts.Select(a => a.id).Distinct().Count() != alerts.Length || attacks.Select(a => a.id).Distinct().Count() != attacks.Length ||
                alerts.Any(a => a.packetIds.Any(id => !ids.Contains(id))) || attacks.Any(a => a.packetIds.Any(id => !ids.Contains(id))) || !packets.Any(p => p.src == attackerIp))
                throw new InvalidDataException("SOC evidence references do not match the training traffic.");
        }
        public static bool TrainingIp(string value)
        {
            var parts = (value ?? "").Split('.');
            if (parts.Length != 4 || parts.Any(p => !int.TryParse(p, out int n) || n < 0 || n > 255)) return false;
            return parts[0] == "10" || value.StartsWith("203.0.113.", StringComparison.Ordinal) || value.StartsWith("198.51.100.", StringComparison.Ordinal) || value.StartsWith("192.0.2.", StringComparison.Ordinal);
        }
    }
    [Serializable] public class SocProgress
    {
        public string missionId, attacker = "", filter = "", selectedPacket = "";
        public string[] triaged = Array.Empty<string>(), classified = Array.Empty<string>(), responses = Array.Empty<string>();
        public bool responseApplied, complete;
        public IncidentReport report = new IncidentReport();
    }

    // Deterministic investigation rules. Firewall and account actions change saved training state only.
    public sealed class SocSession
    {
        readonly SocScenario scenario;
        public SocProgress State { get; }
        public PacketLog[] Visible { get; private set; }
        public string FilterError { get; private set; } = "";
        public int Stage => State.complete ? 5 : State.triaged.Length < scenario.alerts.Length ? 0 : State.attacker != scenario.attackerIp ? 1 :
            State.classified.Length < scenario.attacks.Length ? 2 : !State.responseApplied ? 3 : 4;
        public SocSession(SocScenario scenario, SocProgress state)
        { this.scenario = scenario; State = state; ApplyFilter(state.filter); }
        public bool Triage(string alertId, bool truePositive, out string feedback)
        {
            var alert = scenario.alerts.FirstOrDefault(a => a.id == alertId);
            if (Stage != 0 || alert == null) { feedback = "Inspect and triage the pending alerts first."; return false; }
            if (alert.truePositive != truePositive) { feedback = "Review the alert context and its packet references. Approved maintenance can be a false positive."; return false; }
            if (!State.triaged.Contains(alertId)) State.triaged = State.triaged.Concat(new[] { alertId }).ToArray();
            feedback = truePositive ? "True positive recorded. Preserve this evidence." : "False positive recorded with maintenance context."; return true;
        }
        public bool ApplyFilter(string value)
        {
            string filter = (value ?? "").Trim(); State.filter = filter; FilterError = "";
            IEnumerable<PacketLog> rows = scenario.packets;
            var source = Regex.Match(filter, @"^ip\.src\s*==\s*((?:\d{1,3}\.){3}\d{1,3})$", RegexOptions.IgnoreCase);
            if (filter.Length > 200) FilterError = "Filter is too long. Use an IP, protocol or a short text fragment.";
            else if (source.Success)
            {
                string ip = source.Groups[1].Value;
                if (!SocScenario.TrainingIp(ip)) FilterError = "Use a valid source IP from the fictional traffic table.";
                else rows = rows.Where(p => p.src == ip);
            }
            else if (filter.StartsWith("ip.", StringComparison.OrdinalIgnoreCase) || filter.Contains("==") || filter.Contains("&&"))
                FilterError = "Filter syntax: ip.src == 203.0.113.42. Or use http, dns, or free text; combine filters one at a time.";
            else if (filter.Equals("http", StringComparison.OrdinalIgnoreCase) || filter.Equals("dns", StringComparison.OrdinalIgnoreCase))
                rows = rows.Where(p => string.Equals(p.protocol, filter, StringComparison.OrdinalIgnoreCase));
            else if (filter.Length > 0) rows = rows.Where(p => (p.time + " " + p.src + " " + p.dst + " " + p.protocol + " " + p.info).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            Visible = FilterError.Length > 0 ? Array.Empty<PacketLog>() : rows.ToArray();
            if (!Visible.Any(p => p.id == State.selectedPacket)) State.selectedPacket = "";
            return FilterError.Length == 0;
        }
        public bool SelectPacket(string id)
        {
            if (!Visible.Any(p => p.id == id)) return false;
            State.selectedPacket = id; return true;
        }
        public bool IdentifyAttacker(string ip, out string feedback)
        {
            if (Stage != 1) { feedback = "Finish alert triage before identifying a source."; return false; }
            var packet = Visible.FirstOrDefault(p => p.id == State.selectedPacket);
            if (FilterError.Length > 0 || string.IsNullOrWhiteSpace(State.filter) || packet == null || packet.src != scenario.attackerIp ||
                !scenario.attacks.Any(a => a.packetIds.Contains(packet.id)))
            { feedback = "Apply a filter and select an attack packet to support the source finding."; return false; }
            if ((ip ?? "").Trim() != scenario.attackerIp) { feedback = "This source is not linked to the attack chain. Compare the filtered requests."; return false; }
            State.attacker = ip.Trim(); feedback = "Attacker source confirmed from filtered evidence."; return true;
        }
        public bool Classify(string kind, out string feedback)
        {
            if (Stage != 2) { feedback = "Identify the attacker before tagging the attacks."; return false; }
            if (FilterError.Length > 0 || !Visible.Any(p => p.id == State.selectedPacket))
            { feedback = "Apply a valid filter and select visible attack evidence before tagging it."; return false; }
            var attack = scenario.attacks.FirstOrDefault(a => a.packetIds.Contains(State.selectedPacket));
            if (attack == null) { feedback = "Select an attack request. Ordinary DNS and health checks are noise."; return false; }
            if (!string.Equals(attack.kind, kind, StringComparison.OrdinalIgnoreCase)) { feedback = "Compare the request with ownership, comment output and query boundaries."; return false; }
            if (!State.classified.Contains(attack.id)) State.classified = State.classified.Concat(new[] { attack.id }).ToArray();
            feedback = attack.kind + " evidence tagged."; return true;
        }
        public bool Respond(string[] actions, out string feedback)
        {
            if (Stage != 3) { feedback = "Classify each attack before applying the incident response."; return false; }
            if (actions == null || !new HashSet<string>(actions).SetEquals(scenario.requiredResponses))
            { feedback = "Contain the source, patch the root causes and reset exposed credentials. Preserve the logs."; return false; }
            State.responses = actions.Distinct().ToArray(); State.responseApplied = true;
            feedback = "SIMULATED: firewall blocked " + State.attacker + "; source patches enabled; exposed credentials reset. Evidence retained."; return true;
        }
        public bool SubmitReport(IncidentReport report, out string feedback)
        {
            if (Stage != 4) { feedback = "Complete triage, evidence, classification and response before filing a report."; return false; }
            State.report = report ?? new IncidentReport();
            if (!ValidateReport(State.report, out feedback)) return false;
            State.complete = true; feedback = "Incident report validated against the evidence. Case closed."; return true;
        }
        public bool ValidateReport(IncidentReport report, out string feedback)
        {
            if (report == null || (report.who ?? "").Trim() != scenario.attackerIp) { feedback = "WHO: record the confirmed source IP."; return false; }
            if (scenario.attacks.Any(a => !ContainsTerm(report.what, a.kind))) { feedback = "WHAT: include each attack type you tagged."; return false; }
            if (!(report.when ?? "").Contains(scenario.firstSeen)) { feedback = "WHEN: include the first attack time from the traffic table."; return false; }
            if (scenario.impactTerms.Any(term => !ContainsTerm(report.impact, term))) { feedback = "IMPACT: explain the affected " + string.Join(" and ", scenario.impactTerms) + "."; return false; }
            if (new[] { "block", "patch", "reset" }.Any(term => !ContainsTerm(report.mitigation, term))) { feedback = "MITIGATION: describe the block, patch and reset actions."; return false; }
            feedback = "Report matches the evidence."; return true;
        }
        static bool ContainsTerm(string text, string term) => Regex.IsMatch(text ?? "", @"\b" + Regex.Escape(term) + @"(?:s|es|ed|ing|ting)?\b", RegexOptions.IgnoreCase);
    }
}
