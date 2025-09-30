using CounterStrikeSharp.API;

namespace Tase
{
    public partial class TasePlugin
    {
        partial void Movement_OnTick()
        {
            // No custom per-tick movement adjustments necessary; engine uses config values.
        }

        partial void Movement_OnMapStart(string map)
        {
            // Apply movement-related cvars each map start for GoldSrc-like physics
            Server.ExecuteCommand($"sv_airaccelerate {Config.Movement.AirAccelerate}");
            Server.ExecuteCommand($"sv_airmove {Config.Movement.AirMove}");
        }

        partial void Movement_OnMapEnd(string map)
        {
            // No special cleanup needed for movement on map end
        }
    }
}
