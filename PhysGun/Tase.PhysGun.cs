using System;
using System.Collections.Generic;
using System.Numerics;
using CounterStrikeSharp.API.Core;

namespace Tase
{
    public partial class TasePlugin
    {
        private readonly HashSet<ulong> _physGunEnabled = new();
        private readonly Dictionary<ulong, WeakReference<CCSPlayerController>> _controllerRefs = new();

        private sealed class GrabState
        {
            public Entity Entity = new();
            public int EntityId;
            public bool Frozen;
            public bool Noclip;
            public float HoldDistance;
        }
        private readonly Dictionary<ulong, GrabState> _grabbed = new();

        private const float MinHoldDistance = 48f;
        private const float WheelStep = 32f;

        // ---------------- Commands ----------------

        private void CmdGrab(CCSPlayerController? caller, CommandInfo info)
        {
            if (caller != null && !HasPrivilege(caller, TasePrivilege.UsePhysGun) && !HasAtLeast(caller, TaseRole.GoD))
            {
                SayTo(caller, "You lack permission (need UsePhysGun or GoD).");
                return;
            }
            if (caller == null) { SayTo(null, "Use this in-game."); return; }

            ulong sid = caller.SteamID;
            if (_physGunEnabled.Contains(sid))
            {
                _physGunEnabled.Remove(sid);
                if (_grabbed.TryGetValue(sid, out var g))
                {
                    ReleaseGrab(caller, g);
                    _grabbed.Remove(sid);
                }
                _controllerRefs.Remove(sid);
                SayTo(caller, "[PhysGun] Disabled.");
            }
            else
            {
                _physGunEnabled.Add(sid);
                _controllerRefs[sid] = new WeakReference<CCSPlayerController>(caller);
                SayTo(caller, "[PhysGun] Enabled. LMB=grab, RMB=rotate, R=freeze, Shift=noclip, Wheel=distance.");
            }
        }

        // Push alias profile (on-demand) to let players toggle between "PhysGun mode binds" and their defaults.
        // Usage (client): bind "ALT" "pg_mode"
        // Then press ALT to switch between +pg_* and default combat binds.
        private void CmdPgBinds(CCSPlayerController? caller, CommandInfo _)
        {
            if (caller == null) return;
#if TASE_HAS_CLIENTCMD
            InstallClientBindAliases(caller);
            SayTo(caller, "[PhysGun] Installed alias toggle. Tip: bind \"ALT\" \"pg_mode\"");
#else
            SayTo(caller, "[PhysGun] Client command push disabled in this build.");
            SayTo(caller, "Paste this into your console/autoexec to set up a toggle:");
            SayTo(caller, "alias +pg_m1 +physgun_attack; alias -pg_m1 -physgun_attack");
            SayTo(caller, "alias +pg_m2 +physgun_attack2; alias -pg_m2 -physgun_attack2");
            SayTo(caller, "alias +pg_r +physgun_reload; alias -pg_r -physgun_reload");
            SayTo(caller, "alias +pg_shift +physgun_speed; alias -pg_shift -physgun_speed");
            SayTo(caller, "alias pg_wup +physgun_wup; alias pg_wdown +physgun_wdown");
            SayTo(caller, "alias pg_mode_on \"bind mouse1 +pg_m1; bind mouse2 +pg_m2; bind r +pg_r; bind shift +pg_shift; bind mwheelup pg_wup; bind mwheeldown pg_wdown; alias pg_mode pg_mode_off\"");
            SayTo(caller, "alias pg_mode_off \"bind mouse1 +attack; bind mouse2 +attack2; bind r +reload; bind shift +speed; bind mwheelup invprev; bind mwheeldown invnext; alias pg_mode pg_mode_on\"");
            SayTo(caller, "alias pg_mode pg_mode_on; bind ALT pg_mode");
#endif
        }

        // Convenience server-side trigger: /pgmode will call the client's pg_mode alias (if installed)
        private void CmdPgMode(CCSPlayerController? caller, CommandInfo _)
        {
            if (caller == null) return;
#if TASE_HAS_CLIENTCMD
            try { caller.ExecuteClientCommand("pg_mode"); } catch {}
            SayTo(caller, "[PhysGun] Toggled bind mode (pg_mode).");
#else
            SayTo(caller, "[PhysGun] This build cannot execute client aliases. Use: bind ALT pg_mode");
#endif
        }

        // ---------------- Tick ----------------

        partial void Phys_OnTick()
        {
            if (_physGunEnabled.Count == 0) return;

            foreach (var sid in _physGunEnabled)
            {
                if (!_controllerRefs.TryGetValue(sid, out var wref) || !wref.TryGetTarget(out var player) || player == null)
                    continue;

                bool pressLMB     = InputBridge.WasButtonPressed(player, InputBridge.InputButton.Attack);
                bool releaseLMB   = InputBridge.WasButtonReleased(player, InputBridge.InputButton.Attack);
                bool pressRMB     = InputBridge.WasButtonPressed(player, InputBridge.InputButton.Attack2);
                bool pressReload  = InputBridge.WasButtonPressed(player, InputBridge.InputButton.Reload);
                bool pressShift   = InputBridge.WasButtonPressed(player, InputBridge.InputButton.Speed);
                bool releaseShift = InputBridge.WasButtonReleased(player, InputBridge.InputButton.Speed);
                bool wheelUp      = InputBridge.WasButtonPressed(player, InputBridge.InputButton.WheelUp);
                bool wheelDown    = InputBridge.WasButtonPressed(player, InputBridge.InputButton.WheelDown);

                if (_grabbed.TryGetValue(sid, out var grab))
                {
                    if (wheelUp)   { grab.HoldDistance = MathF.Min(grab.HoldDistance + WheelStep, Config.PhysMaxDistance); SayTo(player, $"[PhysGun] Distance: {grab.HoldDistance:0}"); }
                    if (wheelDown) { grab.HoldDistance = MathF.Max(grab.HoldDistance - WheelStep, MinHoldDistance);       SayTo(player, $"[PhysGun] Distance: {grab.HoldDistance:0}"); }

                    if (releaseLMB)
                    {
                        ReleaseGrab(player, grab);
                        _grabbed.Remove(sid);
                        SayTo(player, "[PhysGun] Released.");
                        continue;
                    }
                    if (pressRMB)
                    {
                        var ang = grab.Entity.GetAngles();
                        ang.Y += 90f;
                        grab.Entity.SetAngles(ang);
                        SayTo(player, $"[PhysGun] Rotated entity {grab.EntityId}.");
                    }
                    if (pressReload && !grab.Frozen)
                    {
                        grab.Frozen = true;
                        grab.Entity.SetFrozen(true);
                        SayTo(player, $"[PhysGun] Froze entity {grab.EntityId}.");
                        ReleaseGrab(player, grab);
                        _grabbed.Remove(sid);
                        continue;
                    }

                    if (!Config.PhysSafeMode)
                    {
                        if (pressShift && !grab.Noclip) { grab.Noclip = true;  grab.Entity.SetCollisionEnabled(false); }
                        if (releaseShift && grab.Noclip){ grab.Noclip = false; grab.Entity.SetCollisionEnabled(true);  }
                    }

                    var eyePos   = GetEyePosition(player);
                    var aimDir   = GetAimDirection(player);
                    float dist   = MathF.Min(MathF.Max(grab.HoldDistance <= 0 ? 150f : grab.HoldDistance, MinHoldDistance), Config.PhysMaxDistance);
                    var target   = eyePos + aimDir * dist;

                    var cur = grab.Entity.GetOrigin();
                    var to  = target - cur;
                    var step = to.Length() < 1f ? to : Vector3.Normalize(to) * MathF.Min(50f, to.Length());
                    grab.Entity.SetOrigin(cur + step);
                }
                else
                {
                    if (pressLMB)
                    {
                        var eye = GetEyePosition(player);
                        var dir = GetAimDirection(player);

                        var tr = Physics.Raycast(eye, dir, Config.PhysMaxDistance); // TODO: real CS2 trace
                        if (tr.Entity != null && tr.Entity.IsValid() && tr.Entity.ClassName != "player")
                        {
                            var g = new GrabState
                            {
                                Entity       = tr.Entity,
                                EntityId     = tr.Entity.EntityId,
                                HoldDistance = tr.Distance <= 0 ? 150f : tr.Distance,
                                Frozen       = false,
                                Noclip       = false
                            };
                            tr.Entity.SetFrozen(true);
                            if (Config.PhysSafeMode)
                            {
                                tr.Entity.SetCollisionEnabled(false);
                                g.Noclip = true;
                            }
                            _grabbed[sid] = g;
                            SayTo(player, $"[PhysGun] Grabbed entity {g.EntityId}.");
                        }
                        else
                        {
                            SayTo(player, "[PhysGun] No valid target.");
                        }
                    }
                }
            }
        }

        partial void Phys_OnPlayerDisconnected(CCSPlayerController player)
        {
            ulong sid = player.SteamID;
            if (_grabbed.TryGetValue(sid, out var g))
            {
                ReleaseGrab(player, g);
                _grabbed.Remove(sid);
            }
            _physGunEnabled.Remove(sid);
            _controllerRefs.Remove(sid);
        }

        partial void Phys_OnMapEnd(string map)
        {
            foreach (var sid in _physGunEnabled)
            {
                if (_grabbed.TryGetValue(sid, out var g))
                {
                    if (!g.Frozen) g.Entity.SetFrozen(false);
                    g.Entity.SetCollisionEnabled(true);
                }
            }
            _grabbed.Clear();
            _physGunEnabled.Clear();
            _controllerRefs.Clear();
        }

        private static void ReleaseGrab(CCSPlayerController actor, GrabState g)
        {
            if (!g.Frozen) g.Entity.SetFrozen(false);
            g.Entity.SetCollisionEnabled(true);
        }

#if TASE_HAS_CLIENTCMD
        // Installs two alias profiles on client and a toggle entrypoint "pg_mode".
        private static void InstallClientBindAliases(CCSPlayerController p)
        {
            void Exec(string cmd) { try { p.ExecuteClientCommand(cmd); } catch {} }

            // Core +pg_* aliases (edge handlers already registered server-side via InputBridge)
            Exec("alias +pg_m1 +physgun_attack");
            Exec("alias -pg_m1 -physgun_attack");
            Exec("alias +pg_m2 +physgun_attack2");
            Exec("alias -pg_m2 -physgun_attack2");
            Exec("alias +pg_r +physgun_reload");
            Exec("alias -pg_r -physgun_reload");
            Exec("alias +pg_shift +physgun_speed");
            Exec("alias -pg_shift -physgun_speed");
            Exec("alias pg_wup +physgun_wup");
            Exec("alias pg_wdown +physgun_wdown");

            // Mode ON: bind combat keys to physgun handlers; then switch entrypoint to OFF
            Exec("alias pg_mode_on \"bind mouse1 +pg_m1; bind mouse2 +pg_m2; bind r +pg_r; bind shift +pg_shift; bind mwheelup pg_wup; bind mwheeldown pg_wdown; alias pg_mode pg_mode_off\"");

            // Mode OFF: restore vanilla CS2 defaults; then switch entrypoint to ON
            // Adjust if your players use different defaults (they can edit after installation)
            Exec("alias pg_mode_off \"bind mouse1 +attack; bind mouse2 +attack2; bind r +reload; bind shift +speed; bind mwheelup invprev; bind mwheeldown invnext; alias pg_mode pg_mode_on\"");

            // Entrypoint starts as ON so first press enables physgun-friendly binds
            Exec("alias pg_mode pg_mode_on");
        }
#endif

        // Aim helpers (replace with CS2 API once available)
        private static Vector3 GetEyePosition(CCSPlayerController player)  => Vector3.Zero;
        private static Vector3 GetAimDirection(CCSPlayerController player) => new Vector3(1, 0, 0);
    }

    // ======= STUBS =======
    internal sealed class Entity
    {
        public int EntityId { get; set; }
        public string ClassName { get; set; } = "prop_physics";
        public bool IsValid() => true;
        private Vector3 _pos = Vector3.Zero;
        private (float X, float Y, float Z) _ang = (0, 0, 0);
        private bool _frozen = false;
        private bool _collide = true;
        public Vector3 GetOrigin() => _pos;
        public void SetOrigin(Vector3 v) { _pos = v; }
        public (float X, float Y, float Z) GetAngles() => _ang;
        public void SetAngles((float X, float Y, float Z) a) { _ang = a; }
        public void SetFrozen(bool v) { _frozen = v; }
        public void SetCollisionEnabled(bool v) { _collide = v; }
    }

    internal static class Physics
    {
        internal sealed class TraceResult { public Entity? Entity; public float Distance; }
        public static TraceResult Raycast(Vector3 start, Vector3 dir, float maxDist)
            => new TraceResult { Entity = null, Distance = 0 };
    }
}
