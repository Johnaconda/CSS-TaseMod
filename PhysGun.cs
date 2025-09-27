// PhysGun.cs
using System;
using System.Collections.Generic;

public static class PhysGun
{
    // Track players who toggled physgun
    private static HashSet<ulong> _activePlayers = new();
    private static Dictionary<int, GrabbedEntity> _grabs = new();

    public class GrabbedEntity
    {
        public int EntityId;
        public ulong GrabberSteam;
        public Vector3 LocalOffset;
        public Quaternion LocalRotation;
        public float Distance;
        public bool Frozen;
    }

    public static void Init()
    {
        // Called at load
        Server.ConsoleWriteLine("[TASE] PhysGun initialized.");
    }

    public static void ToggleForPlayer(PlayerInvoker p)
    {
        if (_activePlayers.Contains(p.SteamId)) { _activePlayers.Remove(p.SteamId); p.Reply("PhysGun: disabled."); DetachIfHolding(p); }
        else { _activePlayers.Add(p.SteamId); p.Reply("PhysGun: enabled. Left-click to grab. Right-click to rotate. Reload to freeze/unfreeze."); }
    }

    public static void OnPlayerPrimaryFire(PlayerInvoker p)
    {
        if (!_activePlayers.Contains(p.SteamId)) return;
        var eye = p.GetEyePosition();
        var dir = p.GetAimDirection();
        var ray = Physics.Raycast(eye, dir, Config.PhysMaxDistance);
        if (ray == null) { p.Reply("No target."); return; }
        var ent = ray.Entity;
        if (!IsEntityMovable(ent)) { p.Reply("Entity not movable."); return; }
        StartGrab(p, ent, ray.HitPos);
    }

    public static void OnPlayerSecondaryFire(PlayerInvoker p)
    {
        // rotate mode - rotate grabbed entity by small delta around local axis
        var g = GetGrabForPlayer(p);
        if (g == null) { p.Reply("No grabbed entity."); return; }
        g.LocalRotation *= Quaternion.FromEuler(0, 15f * (float)Math.PI / 180f, 0); // yaw rotate
        ApplyGrabTransform(g);
    }

    public static void OnPlayerReload(PlayerInvoker p)
    {
        var g = GetGrabForPlayer(p);
        if (g == null) { p.Reply("No grabbed entity."); return; }
        g.Frozen = !g.Frozen;
        if (g.Frozen) { FreezeEntity(g.EntityId); p.Reply("Entity frozen."); }
        else { UnfreezeEntity(g.EntityId); p.Reply("Entity unfrozen and physicalized."); }
        Logger.LogAction("physgun_freeze", p.SteamId, null, $"ent={g.EntityId} frozen={g.Frozen}");
    }

    static void StartGrab(PlayerInvoker p, Entity ent, Vector3 hitPos)
    {
        var id = ent.Id;
        var g = new GrabbedEntity {
            EntityId = id,
            GrabberSteam = p.SteamId,
            Distance = Vector3.Distance(p.GetEyePosition(), hitPos),
            Frozen = false
        };
        g.LocalOffset = ent.Transform.InverseTransformPoint(hitPos);
        g.LocalRotation = ent.Rotation.Inverse() * p.EyeRotation;
        _grabs[id] = g;
        // drop entity physics (kinematic detach) and attach to handle
        MakeKinematic(ent);
        Server.RegisterTickCallback(() => UpdateGrab(g)); // register per-tick updater; remove on release
        p.Reply($"Grabbed entity {id}");
        Logger.LogAction("physgun_grab", p.SteamId, null, $"ent={id} pos={ent.Position}");
    }

    static void DetachIfHolding(PlayerInvoker p)
    {
        var g = _grabs.Values.FirstOrDefault(x => x.GrabberSteam == p.SteamId);
        if (g != null) ReleaseGrab(g);
    }

    static GrabbedEntity GetGrabForPlayer(PlayerInvoker p) => _grabs.Values.FirstOrDefault(x => x.GrabberSteam == p.SteamId);

    static void UpdateGrab(GrabbedEntity g)
    {
        var player = Server.FindPlayerBySteam(g.GrabberSteam);
        if (player == null) { ReleaseGrab(g); return; }
        var eye = player.GetEyePosition();
        var dir = player.GetAimDirection();
        var targetPos = eye + dir * g.Distance;
        // safety: clamp to max distance
        if (Vector3.Distance(eye, targetPos) > Config.PhysMaxDistance) targetPos = eye + dir * Config.PhysMaxDistance;
        var ent = Server.GetEntityById(g.EntityId);
        if (ent == null) { ReleaseGrab(g); return; }
        // move entity smoothly
        ent.SetPosition(Vector3.Lerp(ent.Position, targetPos - ent.Transform.TransformVector(g.LocalOffset), 0.5f));
        ent.SetRotation(Quaternion.Slerp(ent.Rotation, player.EyeRotation * g.LocalRotation, 0.5f));
    }

    static void ReleaseGrab(GrabbedEntity g)
    {
        var ent = Server.GetEntityById(g.EntityId);
        if (ent != null)
        {
            if (!g.Frozen) MakePhysical(ent); // re-enable physics
            Logger.LogAction("physgun_release", g.GrabberSteam, null, $"ent={g.EntityId}");
        }
        _grabs.Remove(g.EntityId);
    }

    static bool IsEntityMovable(Entity e) => e != null && !e.IsWorld && !e.IsPlayer && !e.IsStatic;

    // Helper: make kinematic (no physics response)
    static void MakeKinematic(Entity e) { e.SetPhysicsKinematic(true); }
    static void MakePhysical(Entity e) { e.SetPhysicsKinematic(false); e.WakePhysics(); }
    static void FreezeEntity(int id) { var e = Server.GetEntityById(id); if (e != null) { e.SetPhysicsKinematic(true); e.SetCollision(true); } }
    static void UnfreezeEntity(int id) { var e = Server.GetEntityById(id); if (e != null) { e.SetPhysicsKinematic(false); e.WakePhysics(); } }
}
