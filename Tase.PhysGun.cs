// Tase.PhysGun.cs — Physics Gun (still using stubs for trace/entity)
using System;
using System.Collections.Generic;
using System.Numerics;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

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

        private void CmdGrab(CCSPlayerController? caller, CommandInfo info)
        {
            // Allow if caller has UsePhysGun privilege OR is at least GoD
            if (caller == null) { /* console ok */ }
            else if (!HasPrivilege(caller, TasePrivilege.UsePhysGun) && !HasAtLeast(caller, TaseRole.GoD))
            {
                SayTo(caller, "You lack permission (need UsePhysGun or GoD).");
                return;
            }

            if (caller == null) { SayTo(caller, "Use this in-game."); return; } // no player to attach to
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
                SayTo(caller, "[PhysGun] Enabled. LMB=grab, RMB=rotate, R=freeze, Shift=noclip.");
            }
        }

        partial void Phys_OnTick()
        {
            if (_physGunEnabled.Count == 0) return;

            foreach (var sid in _physGunEnabled)
            {
                if (!_controllerRefs.TryGetValue(sid, out var wr) || !wr.TryGetTarget(out var p) || p is null)
                    continue;

                // PLACEHOLDER INPUTS — wire later
                bool pressLMB   = false;
                bool pressRMB   = false;
                bool pressReload= false;
                bool pressShift = false;

                if (_grabbed.TryGetValue(sid, out var grab))
                {
                    if (pressRMB)
                    {
                        var a = grab.Entity.GetAngles();
                        a.Y += 90f;
                        grab.Entity.SetAngles(a);
                    }
                    if (pressReload && !grab.Frozen)
                    {
                        grab.Frozen = true;
                        grab.Entity.SetFrozen(true);
                        ReleaseGrab(p, grab);
                        _grabbed.Remove(sid);
                        continue;
                    }
                    if (pressShift)
                    {
                        grab.Noclip = !grab.Noclip;
                        grab.Entity.SetCollisionEnabled(!grab.Noclip);
                    }

                    var eyePos = GetEyePosition(p);
                    var aimDir = GetAimDirection(p);
                    var dist   = Math.Min(grab.HoldDistance <= 0 ? 150f : grab.HoldDistance, Config.PhysMaxDistance);
                    var target = eyePos + aimDir * dist;

                    var cur = grab.Entity.GetOrigin();
                    var to  = target - cur;
                    var step = to.Length() < 1f ? to : Vector3.Normalize(to) * MathF.Min(50f, to.Length());
                    grab.Entity.SetOrigin(cur + step);
                }
                else
                {
                    if (pressLMB)
                    {
                        var eye = GetEyePosition(p);
                        var dir = GetAimDirection(p);
                        var tr = Physics.Raycast(eye, dir, Config.PhysMaxDistance); // STUB
                        if (tr.Entity != null && tr.Entity.IsValid() && tr.Entity.ClassName != "player")
                        {
                            var g = new GrabState
                            {
                                Entity = tr.Entity,
                                EntityId = tr.Entity.EntityId,
                                HoldDistance = (tr.Distance <= 0 ? 150f : tr.Distance),
                                Frozen = false,
                                Noclip = false,
                            };
                            tr.Entity.SetFrozen(true);
                            if (Config.PhysSafeMode)
                            {
                                tr.Entity.SetCollisionEnabled(false);
                                g.Noclip = true;
                            }
                            _grabbed[sid] = g;
                            SayTo(p, $"[PhysGun] Grabbed entity {g.EntityId}.");
                        }
                        else
                        {
                            SayTo(p, "[PhysGun] No valid target.");
                        }
                    }
                }
            }
        }

        partial void Phys_OnPlayerDisconnected(CCSPlayerController p)
        {
            ulong sid = p.SteamID;
            if (_grabbed.TryGetValue(sid, out var g))
            {
                ReleaseGrab(p, g);
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
                    if (g.Noclip)  g.Entity.SetCollisionEnabled(true);
                }
            }
            _grabbed.Clear();
            _physGunEnabled.Clear();
            _controllerRefs.Clear();
        }

        private static void ReleaseGrab(CCSPlayerController actor, GrabState g)
        {
            if (!g.Frozen) g.Entity.SetFrozen(false);
            g.Entity.SetCollisionEnabled(!g.Noclip);
        }

        // --------- Position/Aim helpers (still stubs; safe to compile) ----------
        private static Vector3 GetEyePosition(CCSPlayerController p)  => Vector3.Zero;
        private static Vector3 GetAimDirection(CCSPlayerController p) => new Vector3(1, 0, 0);
    }

    // ===========================
    // ======== STUB AREA ========
    // These compile-only stubs let you build now. Replace them with real CS2/CounterStrikeSharp calls later.
    // ===========================
    internal sealed class Entity
    {
        public int EntityId { get; set; }
        public string ClassName { get; set; } = "prop_physics";
        public bool IsValid() => true;
        private Vector3 _pos = Vector3.Zero;
        private (float X,float Y,float Z) _ang = (0,0,0);
        private bool _frozen = false;
        private bool _collide = true;
        public Vector3 GetOrigin() => _pos;
        public void SetOrigin(Vector3 v) { _pos = v; }
        public (float X,float Y,float Z) GetAngles() => _ang;
        public void SetAngles((float X,float Y,float Z) a) { _ang = a; }
        public void SetFrozen(bool v) { _frozen = v; }
        public void SetCollisionEnabled(bool v) { _collide = v; }
    }

    internal static class Physics
    {
        internal sealed class TraceResult
        {
            public Entity? Entity;
            public float Distance;
        }
        public static TraceResult Raycast(Vector3 start, Vector3 dir, float maxDist)
            => new TraceResult { Entity = null, Distance = 0 }; // stub
    }
}
