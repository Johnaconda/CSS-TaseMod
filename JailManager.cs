// JailManager.cs
using System;
using System.Collections.Generic;
using System.IO;

public static class JailManager
{
    public class JailArea { public Vector3 Center; public float Radius; public float Height; }

    private static JailArea _jail = null;
    private static Dictionary<ulong, DateTime> _jailed = new(); // steamId -> jailedAt
    private static string _jailLogPath;

    public static void Init()
    {
        _jailLogPath = Path.Combine(TasePlugin.DataPath, "jail.log");
    }

    public static void SetJailArea(Vector3 center, float radius = 128f, float height = 96f)
    {
        _jail = new JailArea { Center = center, Radius = radius, Height = height };
        Logger.LogAction("set_jail", 0, null, $"center={center},radius={radius},height={height}");
    }

    public static bool JailPlayer(Player target, ulong actorSteam)
    {
        if (_jail == null) { return false; }
        if (TryPlaceInJail(target))
        {
            _jailed[target.SteamId] = DateTime.UtcNow;
            GrantJailPistol(target);
            LogJailAction("jail", actorSteam, target);
            return true;
        }
        return false;
    }

    public static void UnjailPlayer(Player target, ulong actorSteam)
    {
        if (_jailed.Remove(target.SteamId))
        {
            RemoveJailPistol(target);
            LogJailAction("unjail", actorSteam, target);
            target.Reply("You are unjailed.");
        }
    }

    static bool TryPlaceInJail(Player p)
    {
        // try a series of ring positions to avoid collision; we pick up to N tries
        for (int i=0;i<32;i++)
        {
            var angle = (float)(i * (Math.PI*2/32.0));
            var r = 32f + (i%6) * 12f;
            var candidate = _jail.Center + new Vector3(MathF.Cos(angle)*r, MathF.Sin(angle)*r, 8f);
            if (IsSpotClear(candidate, _jail.Height))
            {
                TeleportPlayer(p, candidate);
                return true;
            }
        }
        // fallback: teleport above center, slightly up
        TeleportPlayer(p, _jail.Center + new Vector3(0,0,32f));
        return true;
    }

    static bool IsSpotClear(Vector3 pos, float height)
    {
        var colliders = Physics.OverlapBox(pos, new Vector3(16,16,height/2f));
        return colliders.Length == 0;
    }

    static void TeleportPlayer(Player p, Vector3 pos) { p.SetPosition(pos); p.SetVelocity(Vector3.Zero); }

    static void GrantJailPistol(Player p)
    {
        p.GiveWeapon("weapon_silenced_pistol");
        p.SetSilentWeapon(true);
        p.Reply("You are jailed: you received a silenced pistol. Shooting is logged.");
        // hook shot event
        p.OnWeaponFire += (attacker, weapon, target) =>
        {
            var line = $"{DateTime.UtcNow:o} SHOT actor={attacker.SteamId} weapon={weapon} pos={attacker.Position} target={target?.SteamId}";
            File.AppendAllText(_jailLogPath, line + Environment.NewLine);
            Logger.LogAction("jail_shot", attacker.SteamId, target?.SteamId, $"weapon={weapon},pos={attacker.Position}");
        };
    }

    static void RemoveJailPistol(Player p) { p.RemoveWeapon("weapon_silenced_pistol"); p.OnWeaponFire = null; }

    static void LogJailAction(string action, ulong actor, Player target)
    {
        Logger.LogAction(action, actor, target?.SteamId, $"name={target?.Name},pos={target?.Position}");
    }
}
