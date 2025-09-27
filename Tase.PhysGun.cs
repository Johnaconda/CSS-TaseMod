// Tase.PhysGun.cs — phys-gun (toggle mode; engine hooks TODO)

using System;
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands; // <-- needed for CommandInfo

namespace Tase
{
    public partial class TasePlugin
    {
        private readonly HashSet<ulong> _physGunEnabled = new();

        private void CmdGrab(CCSPlayerController? caller, CommandInfo info)
        {
            if (!HasAtLeast(caller, TaseRole.GoD)) { SayTo(caller, "Only GoD may use phys-gun."); return; }
            if (caller == null) { SayTo(caller, "Players only."); return; }

            if (_physGunEnabled.Contains(caller.SteamID))
            {
                _physGunEnabled.Remove(caller.SteamID);
                SayTo(caller, "[PhysGun] Disabled.");
                LogAction("physgun_disable", caller.SteamID, null, "");
                // TODO: if grabbing something, release it (re-physicalize)
            }
            else
            {
                _physGunEnabled.Add(caller.SteamID);
                SayTo(caller, "[PhysGun] Enabled. (TODO: LMB grab, RMB rotate, R freeze/unfreeze)");
                LogAction("physgun_enable", caller.SteamID, null, $"safe={Config.PhysSafeMode}");
                // TODO: hook input: LMB/RMB/Reload and perform raycast + entity kinematic toggles
            }
        }

        // Hook points to add later:
        // - OnPlayerPrimaryAttack: if _physGunEnabled → raycast to entity, set kinematic, move toward aim point.
        // - OnPlayerSecondaryAttack: rotate grabbed entity.
        // - OnPlayerReload: freeze/unfreeze.
        // - OnTick: smooth position/rotation updates.
        // - Safety: clamp distance to Config.PhysMaxDistance; if PhysSafeMode, limit velocities near players.
    }
}
