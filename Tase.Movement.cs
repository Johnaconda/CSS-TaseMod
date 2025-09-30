using CounterStrikeSharp.API;

namespace Tase
{
    public partial class TasePlugin
    {
        partial void Movement_OnMapStart(string map)
        {
            // GoldSrc-like defaults
            Server.ExecuteCommand($"sv_airaccelerate {Config.AirAccelerate}");
            Server.ExecuteCommand($"sv_airmove {Config.AirMove}");
            // (Optional) future: emulate 128 “sub-tick” feel by additional interpolation in per-tick systems.
        }
    }
}
