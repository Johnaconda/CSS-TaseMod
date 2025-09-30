using System;
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;

namespace Tase
{
    internal static class InputBridge
    {
        internal enum InputButton { Attack, Attack2, Reload, Speed, WheelUp, WheelDown }

        private class PlayerInputState
        {
            public bool AttackDown, AttackPressed, AttackReleased;
            public bool Attack2Down, Attack2Pressed, Attack2Released;
            public bool ReloadPressed;
            public bool SpeedDown, SpeedPressed, SpeedReleased;
            public bool WheelUpPressed, WheelDownPressed;
        }

        private static readonly Dictionary<ulong, PlayerInputState> _state = new();

        internal static void Register(TasePlugin plugin)
        {
            // Primary/secondary/reload/speed (Shift)
            plugin.AddCommand("+physgun_attack",  "PhysGun Primary Fire (internal)",   OnAttackPressed);
            plugin.AddCommand("-physgun_attack",  "PhysGun Primary Fire (internal)",   OnAttackReleased);
            plugin.AddCommand("+physgun_attack2", "PhysGun Secondary Fire (internal)", OnAttack2Pressed);
            plugin.AddCommand("-physgun_attack2", "PhysGun Secondary Fire (internal)", OnAttack2Released);
            plugin.AddCommand("+physgun_reload",  "PhysGun Reload (internal)",         OnReloadPressed);
            plugin.AddCommand("-physgun_reload",  "PhysGun Reload (internal)",         OnReloadReleased);
            plugin.AddCommand("+physgun_speed",   "PhysGun Speed Key (internal)",      OnSpeedPressed);
            plugin.AddCommand("-physgun_speed",   "PhysGun Speed Key (internal)",      OnSpeedReleased);

            // Mouse wheel distance
            plugin.AddCommand("+physgun_wup",     "PhysGun Wheel Up (internal)",       OnWheelUp);
            plugin.AddCommand("-physgun_wup",     "PhysGun Wheel Up (internal)",       (p, i) => { /* no-op */ });
            plugin.AddCommand("+physgun_wdown",   "PhysGun Wheel Down (internal)",     OnWheelDown);
            plugin.AddCommand("-physgun_wdown",   "PhysGun Wheel Down (internal)",     (p, i) => { /* no-op */ });
        }

        // ---- Handlers ----
        private static void OnAttackPressed (CCSPlayerController? p, CommandInfo _) { if (p!=null){ var s=Get(p); s.AttackDown=true;  s.AttackPressed=true; } }
        private static void OnAttackReleased(CCSPlayerController? p, CommandInfo _) { if (p!=null){ var s=Get(p); s.AttackDown=false; s.AttackReleased=true;} }
        private static void OnAttack2Pressed(CCSPlayerController? p, CommandInfo _) { if (p!=null){ var s=Get(p); s.Attack2Down=true; s.Attack2Pressed=true;} }
        private static void OnAttack2Released(CCSPlayerController? p, CommandInfo _){ if (p!=null){ var s=Get(p); s.Attack2Down=false;s.Attack2Released=true;} }
        private static void OnReloadPressed (CCSPlayerController? p, CommandInfo _) { if (p!=null){ var s=Get(p); s.ReloadPressed=true; } }
        private static void OnReloadReleased(CCSPlayerController? p, CommandInfo _) { /* no edge needed */ }
        private static void OnSpeedPressed  (CCSPlayerController? p, CommandInfo _) { if (p!=null){ var s=Get(p); s.SpeedDown=true;   s.SpeedPressed=true; } }
        private static void OnSpeedReleased (CCSPlayerController? p, CommandInfo _) { if (p!=null){ var s=Get(p); s.SpeedDown=false;  s.SpeedReleased=true;} }
        private static void OnWheelUp       (CCSPlayerController? p, CommandInfo _) { if (p!=null){ var s=Get(p); s.WheelUpPressed=true; } }
        private static void OnWheelDown     (CCSPlayerController? p, CommandInfo _) { if (p!=null){ var s=Get(p); s.WheelDownPressed=true; } }

        private static PlayerInputState Get(CCSPlayerController p)
        {
            var id = p.SteamID;
            if (!_state.TryGetValue(id, out var st)) { st = new PlayerInputState(); _state[id]=st; }
            return st;
        }

        // ---- Polling helpers (edge-triggered where appropriate) ----
        internal static bool WasButtonPressed (CCSPlayerController p, InputButton b)
        {
            var s = Get(p);
            bool v = b switch {
                InputButton.Attack   => s.AttackPressed,
                InputButton.Attack2  => s.Attack2Pressed,
                InputButton.Reload   => s.ReloadPressed,
                InputButton.Speed    => s.SpeedPressed,
                InputButton.WheelUp  => s.WheelUpPressed,
                InputButton.WheelDown=> s.WheelDownPressed,
                _ => false
            };
            // reset edges
            if (b == InputButton.Attack)   s.AttackPressed   = false;
            if (b == InputButton.Attack2)  s.Attack2Pressed  = false;
            if (b == InputButton.Reload)   s.ReloadPressed   = false;
            if (b == InputButton.Speed)    s.SpeedPressed    = false;
            if (b == InputButton.WheelUp)  s.WheelUpPressed  = false;
            if (b == InputButton.WheelDown)s.WheelDownPressed= false;
            return v;
        }

        internal static bool WasButtonReleased(CCSPlayerController p, InputButton b)
        {
            var s = Get(p);
            bool v = b switch {
                InputButton.Attack   => s.AttackReleased,
                InputButton.Attack2  => s.Attack2Released,
                InputButton.Speed    => s.SpeedReleased,
                _ => false
            };
            if (b == InputButton.Attack)   s.AttackReleased   = false;
            if (b == InputButton.Attack2)  s.Attack2Released  = false;
            if (b == InputButton.Speed)    s.SpeedReleased    = false;
            return v;
        }

        internal static bool IsButtonDown(CCSPlayerController p, InputButton b)
        {
            var s = Get(p);
            return b switch
            {
                InputButton.Attack  => s.AttackDown,
                InputButton.Attack2 => s.Attack2Down,
                InputButton.Speed   => s.SpeedDown,
                _ => false
            };
        }
    }
}
