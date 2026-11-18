// Pity Sense → Streamer.bot (events feed v1). Ready-made: paste, tick, compile.
//
// SETUP (once, about a minute):
//   1. Streamer.bot → Actions → right-click → Add → name it "Pity Sense".
//   2. In that action: Sub-Actions → right-click → Core → C# → Execute C# Code.
//   3. Paste this whole file over what's there. Tick "Precompile on application start".
//      Click "Compile" (bottom left) — it should say "Compiled successfully" — then "Save and Compile".
//   4. That's it. Pity Sense's triggers are now under Triggers → right-click → Custom → Pity Sense
//      on any of your actions (alerts, sounds, OBS scene changes, chat messages).
//
// It finds Pity Sense by itself (port 47823, or the next ones if that's taken), reconnects
// if Pity Sense restarts, and only ever LISTENS: nothing here can change your pity or count
// a pull. Streamer.bot's log (Logs tab) says when it connects.
//
// TRIGGERS (Custom → Pity Sense) and the arguments each one gives your sub-actions:
//   5★ pulled           %name% %pity% %outcome% (won/lost/guaranteed/featured/standard/pending) %bannerName% %gameName%
//   50/50 won           %name% %bannerName% %gameName% %byName%
//   50/50 lost          %name% %bannerName% %gameName% %byName%
//   Pull counted        %pulls% (1 or 10) %fourStars% %fiveStars% %pityBefore% %pity% %bannerName% %gameName%
//   Soft pity reached   %pity% %softPity% %hardPity% %guaranteed% %bannerName% %gameName%
//   Pity changed        %pity% %hardPity% %softPity% %guaranteed% %pullsAvailable% %bannerName% %gameName%
//   Pull session started   %bannerName% %gameName%
//   Pull session ended     %pulls% %fourStars% %fiveStars% %won5050% %lost5050% %averagePity% %gameName%
//   Connected / Disconnected   %version% (Pity Sense's version, on Connected)
// Every trigger also gets %pitySenseEvent% (the event's type) and %pitySenseJson% (the event as sent).
//
// GLOBAL VARIABLES (for text sources, chat messages, other code; not saved between runs):
//   pitySensePity, pitySenseHardPity, pitySenseSoftPity, pitySenseGuaranteed, pitySenseBanner,
//   pitySenseGame, pitySensePullsAvailable, pitySenseSessionPulls, pitySenseSessionFiveStars,
//   pitySenseLastFiveStar, pitySenseConnected
//   e.g. in a Twitch "Send Message" sub-action: Pity ~pitySensePity~/~pitySenseHardPity~
//
// Feed reference: https://github.com/MaleficZenkai/pity-sense-releases/blob/main/INTEGRATIONS.md

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class CPHInline
{
    const string CATEGORY = "Pity Sense";
    const int FIRST_PORT = 47823, LAST_PORT = 47832;

    // trigger label → event name (the event names are what Streamer.bot stores in your actions: never rename them)
    static readonly string[,] TRIGGERS = {
        { "5★ pulled", "pitysense_five_star" },
        { "50/50 won", "pitysense_5050_won" },
        { "50/50 lost", "pitysense_5050_lost" },
        { "Pull counted", "pitysense_pull" },
        { "Soft pity reached", "pitysense_soft_pity" },
        { "Pity changed", "pitysense_state" },
        { "Pull session started", "pitysense_session_start" },
        { "Pull session ended", "pitysense_session_end" },
        { "Connected", "pitysense_connected" },
        { "Disconnected", "pitysense_disconnected" },
    };

    static CancellationTokenSource stop;
    static Task loop;
    static readonly object gate = new object();
    string lastBannerKey;   // "game/banner", to tell "reached soft pity" from "switched banner"
    int lastPity = -1;

    public void Init()
    {
        for (int i = 0; i < TRIGGERS.GetLength(0); i++)
            CPH.RegisterCustomTrigger(TRIGGERS[i, 0], TRIGGERS[i, 1], new[] { CATEGORY });
        Start();
    }

    public void Dispose()
    {
        lock (gate) { if (stop != null) stop.Cancel(); }
    }

    /** Running the action by hand (or from "Streamer.bot Started") makes sure it's listening, and says how it's doing. */
    public bool Execute()
    {
        Start();
        bool on = CPH.GetGlobalVar<bool>("pitySenseConnected", false);
        CPH.LogInfo(on ? "[Pity Sense] connected — pity " + CPH.GetGlobalVar<int>("pitySensePity", false) : "[Pity Sense] not connected yet — is the Pity Sense app open?");
        return true;
    }

    void Start()
    {
        lock (gate)
        {
            if (loop != null && !loop.IsCompleted && stop != null && !stop.IsCancellationRequested) return;
            stop = new CancellationTokenSource();
            CancellationToken token = stop.Token;
            loop = Task.Run(() => Run(token));
        }
    }

    // ------------------------------------------------------------------ connection
    async Task Run(CancellationToken token)
    {
        int wait = 2;
        bool saidMissing = false;
        while (!token.IsCancellationRequested)
        {
            int port = FindPort();
            if (port == 0)
            {
                if (!saidMissing) { CPH.LogInfo("[Pity Sense] app not found on this PC yet — will keep looking"); saidMissing = true; }
            }
            else
            {
                saidMissing = false;
                bool wasConnected = false;
                try { wasConnected = await Listen(port, token); }
                catch (Exception e) { if (!token.IsCancellationRequested) CPH.LogWarn("[Pity Sense] connection dropped: " + e.Message); }
                if (wasConnected)
                {
                    CPH.SetGlobalVar("pitySenseConnected", false, false);
                    Fire("pitysense_disconnected", "disconnected", null, null);
                    wait = 2;
                }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(wait), token); } catch (TaskCanceledException) { break; }
            wait = Math.Min(wait * 2, 30);
        }
    }

    /** The port Pity Sense answers on (it moves up from 47823 if that one is taken), or 0. */
    static int FindPort()
    {
        for (int port = FIRST_PORT; port <= LAST_PORT; port++)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/api/info");
                req.Timeout = 800; req.ReadWriteTimeout = 800; req.Proxy = null;
                using (var res = (HttpWebResponse)req.GetResponse())
                using (var r = new System.IO.StreamReader(res.GetResponseStream()))
                {
                    if (r.ReadToEnd().Contains("\"Pity Sense\"")) return port;
                }
            }
            catch (Exception) { /* nothing there, or something else */ }
        }
        return 0;
    }

    /** Reads events until the connection closes. True if it got as far as Pity Sense's hello. */
    async Task<bool> Listen(int port, CancellationToken token)
    {
        bool connected = false;
        using (var ws = new ClientWebSocket())
        {
            ws.Options.Proxy = null;
            await ws.ConnectAsync(new Uri("ws://127.0.0.1:" + port + "/events"), token);
            var buffer = new byte[16 * 1024];
            var message = new StringBuilder();
            while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                WebSocketReceiveResult got = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (got.MessageType == WebSocketMessageType.Close) break;
                message.Append(Encoding.UTF8.GetString(buffer, 0, got.Count));
                if (!got.EndOfMessage) continue;
                string raw = message.ToString();
                message.Clear();
                try { if (Handle(raw, port)) connected = true; }
                catch (Exception e) { CPH.LogWarn("[Pity Sense] couldn't read an event: " + e.Message); }
            }
        }
        return connected;
    }

    // ------------------------------------------------------------------ events
    /** One event from the feed → global variables and triggers. True for the hello. */
    bool Handle(string raw, int port)
    {
        var e = Json.Parse(raw) as Dictionary<string, object>;
        if (e == null || Int(e, "v") != 1) return false;   // (a future feed version: not understood here)
        string type = Str(e, "type");
        var args = new Dictionary<string, object>();
        switch (type)
        {
            case "hello":
                CPH.SetGlobalVar("pitySenseConnected", true, false);
                args["version"] = Str(e, "version");
                CPH.LogInfo("[Pity Sense] connected to Pity Sense " + Str(e, "version") + " on port " + port);
                Fire("pitysense_connected", type, raw, args);
                return true;

            case "state":
                if (e.ContainsKey("ready") && !Bool(e, "ready")) return false;
                Common(e, args);
                int pity = Int(e, "pity"), soft = Int(e, "softPity");
                args["pity"] = pity; args["hardPity"] = Int(e, "hardPity"); args["softPity"] = soft;
                args["guaranteed"] = Bool(e, "guaranteed");
                args["pullsAvailable"] = Str(e, "pullsAvailable");
                args["paused"] = Bool(e, "paused");
                var s = Obj(e, "session");
                CPH.SetGlobalVar("pitySensePity", pity, false);
                CPH.SetGlobalVar("pitySenseHardPity", Int(e, "hardPity"), false);
                CPH.SetGlobalVar("pitySenseSoftPity", soft, false);
                CPH.SetGlobalVar("pitySenseGuaranteed", Bool(e, "guaranteed"), false);
                CPH.SetGlobalVar("pitySenseBanner", Str(e, "bannerName"), false);
                CPH.SetGlobalVar("pitySenseGame", Str(e, "gameName"), false);
                CPH.SetGlobalVar("pitySensePullsAvailable", args["pullsAvailable"], false);
                CPH.SetGlobalVar("pitySenseSessionPulls", s != null ? Int(s, "pulls") : 0, false);
                CPH.SetGlobalVar("pitySenseSessionFiveStars", s != null ? Int(s, "fiveStars") : 0, false);
                var l5 = Obj(e, "lastFiveStar");
                if (l5 != null) CPH.SetGlobalVar("pitySenseLastFiveStar", Str(l5, "name"), false);
                Fire("pitysense_state", type, raw, args);
                // into soft pity on the same banner (not by switching to a banner that's already there)
                string key = Str(e, "game") + "/" + Str(e, "banner");
                if (soft > 0 && key == lastBannerKey && lastPity >= 0 && lastPity < soft && pity >= soft)
                    Fire("pitysense_soft_pity", "soft_pity", raw, new Dictionary<string, object>(args));
                lastBannerKey = key; lastPity = pity;
                return false;

            case "pull":
                Common(e, args);
                args["pulls"] = Int(e, "pulls"); args["fourStars"] = Int(e, "fourStars"); args["fiveStars"] = Int(e, "fiveStars");
                args["pityBefore"] = Int(e, "pityBefore"); args["pity"] = Int(e, "pity");
                args["exact"] = Bool(e, "exact"); args["source"] = Str(e, "source");
                Fire("pitysense_pull", type, raw, args);
                return false;

            case "five_star":
                Common(e, args);
                args["name"] = Str(e, "name"); args["pity"] = Int(e, "pity"); args["outcome"] = Str(e, "outcome");
                Fire("pitysense_five_star", type, raw, args);
                return false;

            case "fifty_fifty":
                Common(e, args);
                args["name"] = Str(e, "name"); args["byName"] = Bool(e, "byName");
                Fire(Bool(e, "won") ? "pitysense_5050_won" : "pitysense_5050_lost", type, raw, args);
                return false;

            case "session_start":
                Common(e, args);
                Fire("pitysense_session_start", type, raw, args);
                return false;

            case "session_end":   // (a session can span banners: the game only)
                args["game"] = Str(e, "game"); args["gameName"] = Str(e, "gameName");
                args["pulls"] = Int(e, "pulls"); args["fourStars"] = Int(e, "fourStars"); args["fiveStars"] = Int(e, "fiveStars");
                args["won5050"] = Int(e, "won5050"); args["lost5050"] = Int(e, "lost5050");
                args["averagePity"] = Str(e, "averagePity");
                Fire("pitysense_session_end", type, raw, args);
                return false;

            default:
                return false;   // (a newer event this file doesn't know yet: ignored)
        }
    }

    static void Common(Dictionary<string, object> e, Dictionary<string, object> args)
    {
        args["game"] = Str(e, "game"); args["gameName"] = Str(e, "gameName");
        args["banner"] = Str(e, "banner"); args["bannerName"] = Str(e, "bannerName");
    }

    void Fire(string eventName, string type, string raw, Dictionary<string, object> args)
    {
        if (args == null) args = new Dictionary<string, object>();
        args["pitySenseEvent"] = type;
        args["pitySenseJson"] = raw ?? "";
        try { CPH.TriggerCodeEvent(eventName, args); }
        catch (Exception x) { CPH.LogWarn("[Pity Sense] " + eventName + ": " + x.Message); }
    }

    // ------------------------------------------------------------------ reading fields
    static object Get(Dictionary<string, object> o, string k) { object v; return o != null && o.TryGetValue(k, out v) ? v : null; }
    static Dictionary<string, object> Obj(Dictionary<string, object> o, string k) { return Get(o, k) as Dictionary<string, object>; }
    static string Str(Dictionary<string, object> o, string k)
    {
        object v = Get(o, k);
        if (v == null) return "";
        if (v is double) return ((double)v).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (v is bool) return (bool)v ? "true" : "false";
        return v as string ?? "";
    }
    static int Int(Dictionary<string, object> o, string k) { object v = Get(o, k); return v is double ? (int)Math.Round((double)v) : 0; }
    static bool Bool(Dictionary<string, object> o, string k) { object v = Get(o, k); return v is bool && (bool)v; }
}

/**
 * A small JSON reader, so this file needs nothing beyond .NET itself (whatever JSON library
 * Streamer.bot ships with now or later). Objects → Dictionary<string, object>, arrays →
 * List<object>, numbers → double, true/false → bool, null → null.
 */
public static class Json
{
    public static object Parse(string s) { int i = 0; object v = Value(s, ref i); Space(s, ref i); if (i != s.Length) throw new FormatException("extra text after JSON"); return v; }

    static void Space(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static object Value(string s, ref int i)
    {
        Space(s, ref i);
        if (i >= s.Length) throw new FormatException("JSON ended early");
        char c = s[i];
        if (c == '{')
        {
            var o = new Dictionary<string, object>();
            i++; Space(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return o; }
            while (true)
            {
                Space(s, ref i);
                string k = Text(s, ref i);
                Space(s, ref i);
                Expect(s, ref i, ':');
                o[k] = Value(s, ref i);
                Space(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                Expect(s, ref i, '}');
                return o;
            }
        }
        if (c == '[')
        {
            var a = new List<object>();
            i++; Space(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return a; }
            while (true)
            {
                a.Add(Value(s, ref i));
                Space(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                Expect(s, ref i, ']');
                return a;
            }
        }
        if (c == '"') return Text(s, ref i);
        if (Word(s, ref i, "true")) return true;
        if (Word(s, ref i, "false")) return false;
        if (Word(s, ref i, "null")) return null;
        int start = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        double d;
        if (i > start && double.TryParse(s.Substring(start, i - start), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
        throw new FormatException("unexpected '" + c + "' in JSON");
    }

    static bool Word(string s, ref int i, string w)
    {
        if (string.CompareOrdinal(s, i, w, 0, w.Length) != 0) return false;
        i += w.Length; return true;
    }

    static void Expect(string s, ref int i, char c)
    {
        if (i >= s.Length || s[i] != c) throw new FormatException("expected '" + c + "' in JSON");
        i++;
    }

    static string Text(string s, ref int i)
    {
        Expect(s, ref i, '"');
        var b = new StringBuilder();
        while (i < s.Length)
        {
            char c = s[i++];
            if (c == '"') return b.ToString();
            if (c != '\\') { b.Append(c); continue; }
            if (i >= s.Length) break;
            char e = s[i++];
            switch (e)
            {
                case 'n': b.Append('\n'); break;
                case 't': b.Append('\t'); break;
                case 'r': b.Append('\r'); break;
                case 'b': b.Append('\b'); break;
                case 'f': b.Append('\f'); break;
                case 'u':
                    if (i + 4 > s.Length) throw new FormatException("bad \\u escape in JSON");
                    b.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                default: b.Append(e); break;   // \" \\ \/
            }
        }
        throw new FormatException("unterminated string in JSON");
    }
}
