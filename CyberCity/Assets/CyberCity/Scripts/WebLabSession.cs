using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CyberCity
{
    [Serializable] public class WebScenario
    {
        public string kind, host, title, path, ownId, targetId, ownRecord, privateRecord;
        public string description, vulnerableCode, patchedCode, patchLabel, debrief;
        public string[] payloadPatterns, hints;
        public string StartUrl => "https://" + host + path + (kind == "idor" ? "?id=" + ownId : "");

        public void Validate(string expectedKind)
        {
            if (kind != expectedKind || Array.IndexOf(new[] { "idor", "xss", "sqli" }, kind) < 0 ||
                string.IsNullOrEmpty(host) || !Regex.IsMatch(host, @"^[a-z0-9.-]+\.example$") ||
                string.IsNullOrEmpty(path) || !path.StartsWith("/") || path.Contains("?") ||
                string.IsNullOrEmpty(title) || string.IsNullOrEmpty(vulnerableCode) || string.IsNullOrEmpty(patchedCode) ||
                string.IsNullOrEmpty(patchLabel) || string.IsNullOrEmpty(debrief) || hints == null || hints.Length != 2)
                throw new InvalidDataException("Invalid fictional browser scenario.");
            if (kind == "idor" && (string.IsNullOrEmpty(ownId) || string.IsNullOrEmpty(targetId) || ownId == targetId))
                throw new InvalidDataException("Profile scenario needs different own and target IDs.");
            if (kind != "idor" && (payloadPatterns == null || payloadPatterns.Length == 0 || payloadPatterns.Length > 8))
                throw new InvalidDataException("Browser scenario needs payload patterns.");
            foreach (string pattern in payloadPatterns ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(pattern) || pattern.Length > 256) throw new InvalidDataException("Invalid payload pattern.");
                try { _ = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromMilliseconds(50)); }
                catch (ArgumentException) { throw new InvalidDataException("Invalid payload pattern syntax."); }
            }
        }
    }

    [Serializable] public class WebProgress
    {
        public string missionId, kind, attackUrl, attackInput, attackPassword;
        public bool demonstrated, patched, verified;
        public int hints;
    }

    public sealed class WebResponse
    {
        public int status;
        public string page, network;
        public bool fakeAlert;
    }

    // A local router and pattern model. URLs are parsed as data; nothing is fetched or executed.
    public sealed class WebLabSession
    {
        readonly WebScenario scenario;
        public WebProgress State { get; }
        public WebLabSession(WebScenario scenario, WebProgress state) { this.scenario = scenario; State = state; }
        public bool ApplyPatch()
        {
            if (!State.demonstrated) return false;
            State.patched = true; return true;
        }
        public WebResponse Request(string url, string input = "", string password = "training")
        {
            input = (input ?? "").Trim();
            password = password ?? "";
            if (input.Length > 512 || password.Length > 512 || (url ?? "").Length > 512) return Response(400, "Training input is limited to 512 characters.", "Request rejected locally.");
            if ((url ?? "").StartsWith("/")) url = "https://" + scenario.host + url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || uri.Scheme != "https" || uri.Host != scenario.host || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort)
                return Response(400, "Only https://" + scenario.host + " is available in this fictional browser.", "No network request was made.");
            string route = Uri.UnescapeDataString(uri.AbsolutePath);
            if (route != scenario.path) return Response(404, "Training page not found. Reload the scenario home page.", "GET " + uri.PathAndQuery + " -> 404");
            string canonical = uri.AbsoluteUri;
            bool attack = false; string page, query = ""; int status = 200;
            if (scenario.kind == "idor")
            {
                string id = "";
                foreach (string pair in uri.Query.TrimStart('?').Split('&'))
                { var kv = pair.Split(new[] { '=' }, 2); if (kv[0] == "id" && kv.Length == 2) id = Uri.UnescapeDataString(kv[1]); }
                if (id != scenario.ownId && id != scenario.targetId) return Response(404, "No such fictional profile.", "GET " + uri.PathAndQuery + " -> 404");
                attack = id == scenario.targetId;
                status = attack && State.patched ? 403 : 200;
                page = status == 403 ? "403 FORBIDDEN\nOwnership check denied another user's record." : attack ? scenario.privateRecord : scenario.ownRecord;
            }
            else
            {
                foreach (string pattern in scenario.payloadPatterns)
                {
                    try { if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromMilliseconds(50))) { attack = true; break; } }
                    catch (RegexMatchTimeoutException) { return Response(400, "Pattern inspection timed out. Use a shorter training payload.", "Local pattern limit reached."); }
                }
                if (scenario.kind == "xss")
                {
                    page = attack && !State.patched ? "SIMULATED ALERT\nAn unsafe comment reached a code context. This alert is a game panel; no script runs." :
                        State.patched ? "SAFE TEXT OUTPUT\n" + Encode(input) + "\nThe comment is displayed as data. No alert." : "Comment preview: " + input;
                }
                else
                {
                    query = State.patched ? "SELECT * FROM users WHERE name = @name\nBound @name = " + input : "SELECT * FROM users WHERE name = '" + input + "' AND password = '[fictional]'";
                    status = attack && !State.patched ? 200 : 401;
                    page = status == 200 ? "SIMULATED ADMIN SESSION\nThe concatenated query was bypassed. No database is connected." : "401 LOGIN DENIED\nInput did not authenticate a fictional account.";
                }
            }
            if (attack && !State.patched)
            { State.demonstrated = true; State.attackUrl = canonical; State.attackInput = input; State.attackPassword = password; }
            bool sameRequest = canonical == State.attackUrl && input == State.attackInput && password == State.attackPassword;
            if (attack && State.patched && State.demonstrated && sameRequest) State.verified = true;
            string method = scenario.kind == "idor" ? "GET " : "POST ";
            return new WebResponse { status = status, page = page, fakeAlert = scenario.kind == "xss" && attack && !State.patched,
                network = method + uri.PathAndQuery + "\n" + (input.Length > 0 ? "Body: " + input + "\n" : "") + "Response: " + status + (State.patched ? " / patched" : " / vulnerable") + (query.Length > 0 ? "\n" + query : "") };
        }
        static WebResponse Response(int status, string page, string network) => new WebResponse { status = status, page = page, network = network };
        static string Encode(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
    }
}
