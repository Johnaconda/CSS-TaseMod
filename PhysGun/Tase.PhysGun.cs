using System;
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

#if TASE_HAS_CLIENTCMD
                // Restore user’s normal binds (best-effort)
                PushClientBinds(caller, enable:false);
#endif
            }
            else
            {
                _physGunEnabled.Add(sid);
                _controllerRefs[sid] = new WeakReference<CCSPlayerController>(caller);
                SayTo(caller, "[PhysGun] Enabled. LMB=grab, RMB=rotate, R=freeze, Shift=noclip, Wheel=distance.");

#if TASE_HAS_CLIENTCMD
                // Install temporary binds/aliases on client (opt-in; best-effort)
                PushClientBinds(caller, enable:true);
#endif
            }
        }

#if TASE_HAS_CLIENTCMD
        private static void PushClientBinds(CCSPlayerController p, bool enable)
        {
            void Exec(string cmd) { try { p.ExecuteClientCommand(cmd); } catch { } }
            if (enable)
            {
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

                // Note: we don't override their combat binds; users can bind these in a cfg if they want.
                // Provide a hint instead:
                TasePlugin.SayTo(p, "[PhysGun] Tip: bind MOUSE1 \"+pg_m1\"; MOUSE2 \"+pg_m2\"; R \"+pg_r\"; SHIFT \"+pg_shift\"; MWHEELUP \"pg_wup\"; MWHEELDOWN \"pg_wdown\"");
            }
            else
            {
                // Clear aliases (no unbinds to avoid clobbering user settings)
                Exec("alias +pg_m1 \"\"");
                Exec("alias -pg_m1 \"\"");
                Exec("alias +pg_m2 \"\"");
                Exec("alias -pg_m2 \"\"");
                Exec("alias +pg_r \"\"");
                Exec("alias -pg_r \"\"");
                Exec("alias +pg_shift \"\"");
                Exec("alias -pg_shift \"\"");
                Exec("alias pg_wup \"\"");
                Exec("alias pg_wdown \"\"");
            }
        }
#endif

        partial void Phys_OnTick()
        {
            if (_physGunEnabled.Count == 0) return;

            foreach (var sid in _physGunEnabled)
            {
                if (!_controllerRefs.TryGetValue(sid, out var wref) || !wref.TryGetTarget(out var player) || player == null)
                    continue;

                // inputs
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
                    // Adjust distance via wheel
                    if (wheelUp)
                    {
                        grab.HoldDistance = MathF.Min(grab.HoldDistance + WheelStep, Config.PhysMaxDistance);
                        SayTo(player, $"[PhysGun] Distance: {grab.HoldDistance:0}");
                    }
                    if (wheelDown)
                    {
                        grab.HoldDistance = MathF.Max(grab.HoldDistance - WheelStep, MinHoldDistance);
                        SayTo(player, $"[PhysGun] Distance: {grab.HoldDistance:0}");
                    }

                    // Release
                    if (releaseLMB)
                    {
                        ReleaseGrab(player, grab);
                        _grabbed.Remove(sid);
                        SayTo(player, "[PhysGun] Released.");
                        continue;
                    }

                    // Rotate (90° yaw step)
                    if (pressRMB)
                    {
                        var ang = grab.Entity.GetAngles();
                        ang.Y += 90f;
                        grab.Entity.SetAngles(ang);
                        SayTo(player, $"[PhysGun] Rotated entity {grab.EntityId}.");
                    }

                    // Freeze
                    if (pressReload && !grab.Frozen)
                    {
                        grab.Frozen = true;
                        grab.Entity.SetFrozen(true);
                        SayTo(player, $"[PhysGun] Froze entity {grab.EntityId}.");
                        ReleaseGrab(player, grab);
                        _grabbed.Remove(sid);
                        continue;
                    }

                    // Optional noclip passthrough while holding (disabled if SafeMode true, since SafeMode already disables collisions)
                    if (!Config.PhysSafeMode)
                    {
                        if (pressShift && !grab.Noclip) { grab.Noclip = true;  grab.Entity.SetCollisionEnabled(false); }
                        if (releaseShift && grab.Noclip){ grab.Noclip = false; grab.Entity.SetCollisionEnabled(true);  }
                    }

                    // Move toward aim point (server-authoritative)
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

                        // TODO: replace stub with CS2 raycast API when available
                        var tr = Physics.Raycast(eye, dir, Config.PhysMaxDistance);
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

        // Aim helpers (replace with CS2 API once available)
        private static Vector3 GetEyePosition(CCSPlayerController player)  => Vector3.Zero;
        private static Vector3 GetAimDirection(CCSPlayerController player) => new Vector3(1, 0, 0);
    }

    // ===== STUBS (keep until wired to engine) =====
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
