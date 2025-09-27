// Tase.Appeal.cs — ban appeal flow + rotten tomatoes gag (placeholders for spawn)

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CounterStrikeSharp.API;                         // <-- Server (if needed)
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;        // <-- CommandInfo
using CounterStrikeSharp.API.Modules.Utils;           // <-- Utilities.GetPlayers
using Newtonsoft.Json;

namespace Tase
{
    public class AppealSession
    {
        public ulong SteamId { get; set; }
        public string Reason { get; set; } = "";
        public DateTime BannedAt { get; set; } = DateTime.UtcNow;
        public bool Active { get; set; } = true;
        public string ApologyText { get; set; } = "";
    }

    public partial class TasePlugin
    {
        private Dictionary<ulong, AppealSession> _appeals = new();

        internal void LoadAppeals()
        {
            try
            {
                var json = File.ReadAllText(AppealsPath);
                _appeals = JsonConvert.DeserializeObject<Dictionary<ulong, AppealSession>>(json) ?? new();
            }
            catch { _appeals = new(); }
        }

        internal void SaveAppeals()
        {
            File.WriteAllText(AppealsPath, JsonConvert.SerializeObject(_appeals, Formatting.Indented));
        }

        // Call this from your ban flow when someone is banned.
        internal void OnPlayerBanned(ulong steamId, string reason)
        {
            _appeals[steamId] = new AppealSession
            {
                SteamId = steamId,
                Reason = reason,
                BannedAt = DateTime.UtcNow,
                Active = true
            };
            SaveAppeals();
            LogAction("ban", null, steamId, $"reason={reason}");
        }

        // Player command: /appeal <message>
        private void CmdAppeal(CCSPlayerController? caller, CommandInfo info)
        {
            if (caller == null) return;
            var msg = info.ArgCount > 1
                ? string.Join(' ', Enumerable.Range(1, info.ArgCount - 1).Select(i => i < info.ArgCount ? info.GetArg(i) : string.Empty))
                : string.Empty;

            if (!_appeals.TryGetValue(caller.SteamID, out var s))
            {
                // create a pending record if not present
                s = new AppealSession { SteamId = caller.SteamID, Reason = "(manual)", Active = true };
                _appeals[caller.SteamID] = s;
            }

            s.ApologyText = msg;
            SaveAppeals();
            LogAction("appeal_submitted", caller.SteamID, null, $"text_len={msg?.Length ?? 0}");
            SayTo(caller, "[Appeal] Submitted. Admins will review.");

            // Notify Admin+
            foreach (var p in Utilities.GetPlayers())
            {
                if (p == null || !p.IsValid) continue;
                if (HasAtLeast(p, TaseRole.Admin))
                    SayTo(p, $"[Appeal] {caller.PlayerName} ({caller.SteamID}) wrote: {msg}");
            }
        }

        // Admin: /appeals (list)
        private void CmdAppealsList(CCSPlayerController? caller, CommandInfo info)
        {
            if (!HasAtLeast(caller, TaseRole.Admin)) { SayTo(caller, "Admin+ only."); return; }
            var active = _appeals.Values.Where(a => a.Active).OrderBy(a => a.BannedAt).ToList();
            if (active.Count == 0) { SayTo(caller, "[Appeal] No active appeals."); return; }
            foreach (var a in active)
                SayTo(caller, $"{a.SteamId} since {a.BannedAt:u} — \"{Short(a.ApologyText, 80)}\"");
            SayTo(caller, "Use: /appeal_accept <steam64>  |  /appeal_reject <steam64>");
        }

        private void CmdAppealAccept(CCSPlayerController? caller, CommandInfo info)
        {
            if (!HasAtLeast(caller, TaseRole.Admin)) { SayTo(caller, "Admin+ only."); return; }
            if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var sid)) { SayTo(caller, "Usage: /appeal_accept <steam64>"); return; }
            if (_appeals.TryGetValue(sid, out var s))
            {
                s.Active = false; SaveAppeals();
                // TODO: Unban sid via your ban system
                LogAction("appeal_accepted", caller?.SteamID, sid, "");
                SayTo(caller, $"[Appeal] Unban accepted for {sid}.");
            }
            else SayTo(caller, "No such appeal.");
        }

        private void CmdAppealReject(CCSPlayerController? caller, CommandInfo info)
        {
            if (!HasAtLeast(caller, TaseRole.Admin)) { SayTo(caller, "Admin+ only."); return; }
            if (info.ArgCount < 2 || !ulong.TryParse(info.GetArg(1), out var sid)) { SayTo(caller, "Usage: /appeal_reject <steam64>"); return; }
            if (_appeals.TryGetValue(sid, out var s))
            {
                // Keep it active (rejected but still banned)
                LogAction("appeal_rejected", caller?.SteamID, sid, "");
                SayTo(caller, $"[Appeal] Rejected for {sid}. Summoning tomatoes (TODO spawn).");
                // TODO: spawn “pole” and ragdoll copy, give tomatoes to players
            }
            else SayTo(caller, "No such appeal.");
        }

        private static string Short(string? s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }
    }
}
