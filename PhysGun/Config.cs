// BanAppeal.cs
using System;
using System.Collections.Generic;
using System.IO;

public static class BanAppeal
{
    private static Dictionary<ulong, AppealSession> _appeals = new();
    private static string _appealsPath;

    public class AppealSession
    {
        public ulong SteamId;
        public string Reason;
        public DateTime BannedAt;
        public bool Active;
        public string ApologyText;
    }

    public static void Init()
    {
        _appealsPath = Path.Combine(TasePlugin.DataPath, "appeals.json");
        Load();
    }

    static void Load()
    {
        if (!File.Exists(_appealsPath)) return;
        var json = File.ReadAllText(_appealsPath);
        _appeals = JsonConvert.DeserializeObject<Dictionary<ulong, AppealSession>>(json);
    }

    static void Save() { File.WriteAllText(_appealsPath, JsonConvert.SerializeObject(_appeals, Formatting.Indented)); }

    public static void OnBan(ulong steamId, string reason)
    {
        var s = new AppealSession { SteamId = steamId, Reason = reason, BannedAt = DateTime.UtcNow, Active = true };
        _appeals[steamId] = s;
        Save();
        Logger.LogAction("ban", 0, steamId, $"reason={reason}");
    }

    // Called when a banned player connects and is forced to spectate+muted
    public static void OfferAppealOptions(PlayerInvoker spectator)
    {
        spectator.ForceSpectateMuted();
        spectator.Reply("You are banned. Use /appeal <message> to submit a written apology, or disconnect.");
        // optionally show a small in-game UI form for apology text
    }

    public static void SubmitAppeal(PlayerInvoker spectator, string text)
    {
        if (!_appeals.TryGetValue(spectator.SteamId, out var s)) { spectator.Reply("No active ban record found."); return; }
        s.ApologyText = text;
        Save();
        Logger.LogAction("appeal_submitted", spectator.SteamId, null, $"text={text.Truncate(256)}");
        // notify online admins/GoD
        foreach (var p in Server.GetOnlinePlayers())
        {
            if (RolesManager.HasAtLeast(p.SteamId, TaseRole.Admin))
            {
                p.Reply($"[APPEAL] {spectator.Name} ({spectator.SteamId}) submitted an apology: {text}");
                // give clickable options (panorama) to accept/reject
            }
        }
        spectator.Reply("Appeal submitted. Wait for admins to review. You remain in spectate muted.");
    }

    public static void AdminDecideAccept(ulong adminSteam, ulong targetSteam)
    {
        if (!_appeals.TryGetValue(targetSteam, out var s)) return;
        // Perform unban and allow them back
        Server.UnbanPlayer(targetSteam);
        s.Active = false;
        Save();
        Logger.LogAction("appeal_accepted", adminSteam, targetSteam, "");
        // message
        Server.Broadcast($"{Server.GetPlayerNameBySteam(targetSteam)}'s ban has been revoked by admin.");
    }

    public static void AdminDecideReject(ulong adminSteam, ulong targetSteam)
    {
        Logger.LogAction("appeal_rejected", adminSteam, targetSteam, "");
        // optionally spawn the "pole" and rotten tomatoes
        SpawnPoleAndTomatoes(targetSteam, adminSteam);
    }

    static void SpawnPoleAndTomatoes(ulong targetSteam, ulong actorSteam)
    {
        var pos = Server.GetDesignatedPunishLocation(); // e.g. center of map or configured spot
        var copy = Server.SpawnEntity("ragdoll_copy", pos); // spawn a copy of the player's body
        var pole = Server.SpawnEntity("wooden_pole", pos + new Vector3(0,0,-32f));
        // attach copy to pole (parenting)
        copy.SetParent(pole);
        Logger.LogAction("spawn_pole", actorSteam, targetSteam, $"pole_entity={pole.Id}, copy={copy.Id}");
        // spawn throwable 'tomato' items with limited ammo to all online players
        foreach (var p in Server.GetOnlinePlayers())
        {
            if (p.SteamId == actorSteam) continue;
            p.GiveThrowable("item_tomato", Config.TomatoCount);
            p.Reply("Tomato throw: hit the pole to express displeasure!");
        }
    }
}
