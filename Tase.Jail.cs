// Tase.Jail.cs — jail system (safe spawn + pistol logging placeholders)

using System;
using System.IO;
using System.Collections.Generic;
using CounterStrikeSharp.API;                    // <-- Server
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;   // <-- CommandInfo

namespace Tase
{
    public partial class TasePlugin
    {
        private readonly HashSet<ulong> _jailed = new();    // who is jailed
        private string? _jailMarkerNote = null;              // textual marker for now (map/pos TODO)

        private void CmdSetJail(CCSPlayerController? caller, CommandInfo info)
        {
            if (!HasAtLeast(caller, TaseRole.BlockBuster)) { SayTo(caller, "No permission (BlockBuster+)."); return; }
            // TODO: record caller position/orientation; for now just mark map/time
            _jailMarkerNote = $"{Server.MapName} @ {DateTime.UtcNow:o}";
            SayTo(caller, $"[Jail] Set jail marker at {_jailMarkerNote} (TODO: store actual coords).");
            LogAction("set_jail", caller?.SteamID, null, $"marker={_jailMarkerNote}");
        }

        private void CmdJail(CCSPlayerController? caller, CommandInfo info)
        {
            if (!HasAtLeast(caller, TaseRole.BlockBuster)) { SayTo(caller, "No permission (BlockBuster+)."); return; }
            if (info.ArgCount < 2) { SayTo(caller, "Usage: /jail <playerName>"); return; }
            var target = FindByName(info.GetArg(1));
            if (target == null) { SayTo(caller, "Player not found."); return; }

            _jailed.Add(target.SteamID);

            // TODO: Teleport target to jail coords, give silenced pistol, hook OnWeaponFire for logging.
            SayTo(target, "[Jail] You are jailed. Shots are logged. (TODO: pistol + teleport)");
            SayTo(caller, $"[Jail] Jailed {target.PlayerName}.");
            LogAction("jail", caller?.SteamID, target.SteamID, $"marker={_jailMarkerNote ?? "unset"}");
        }

        private void CmdUnjail(CCSPlayerController? caller, CommandInfo info)
        {
            if (!HasAtLeast(caller, TaseRole.BlockBuster)) { SayTo(caller, "No permission (BlockBuster+)."); return; }
            if (info.ArgCount < 2) { SayTo(caller, "Usage: /unjail <playerName>"); return; }
            var target = FindByName(info.GetArg(1));
            if (target == null) { SayTo(caller, "Player not found."); return; }

            _jailed.Remove(target.SteamID);

            // TODO: strip jail pistol and release to normal play/spawn.
            SayTo(target, "[Jail] You are unjailed.");
            SayTo(caller, $"[Jail] Unjailed {target.PlayerName}.");
            LogAction("unjail", caller?.SteamID, target.SteamID, "");
        }
    }
}
