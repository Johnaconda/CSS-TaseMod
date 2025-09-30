using System.Collections.Generic;
using CounterStrikeSharp.API.Core;

namespace Tase
{
    internal static class HUD
    {
        private static readonly HashSet<ulong> _bmPanelOpen = new();

        internal static void OpenBlockMakerPanel(CCSPlayerController player)
        {
            ulong id = player.SteamID;
            bool isOpen = _bmPanelOpen.Contains(id);
#if TASE_HAS_CLIENTCMD
            void Exec(string cmd) { try { player.ExecuteClientCommand(cmd); } catch { } }
#endif
            if (!isOpen)
            {
#if TASE_HAS_CLIENTCMD
                // If your build supports client 'script', this will work:
                Exec("script TASE_BM_Toggle()");
#endif
                _bmPanelOpen.Add(id);
                TasePlugin.SayTo(player, "[TASE] BlockMaker panel requested.");
                TasePlugin.SayTo(player, "[TASE] Bind suggestion: bind \"TAB\" \"script TASE_BM_Toggle()\"");
            }
            else
            {
#if TASE_HAS_CLIENTCMD
                Exec("script TASE_BM_Toggle()");
#endif
                _bmPanelOpen.Remove(id);
                TasePlugin.SayTo(player, "[TASE] BlockMaker panel toggled.");
            }
        }
    }
}
