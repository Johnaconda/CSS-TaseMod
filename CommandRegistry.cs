// 📄 src/Tase/CommandRegistry.cs
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;

namespace Tase
{
    internal static class CommandRegistry
    {
        internal class CommandEntry
        {
            public string Name;
            public string Usage;
            public string Description;
            public TaseRole MinRole;
            public TasePrivilege RequiredPriv;
            public CommandEntry(string name, string usage, string description, TaseRole minRole = TaseRole.None, TasePrivilege requiredPriv = TasePrivilege.None)
            {
                Name = name;
                Usage = usage;
                Description = description;
                MinRole = minRole;
                RequiredPriv = requiredPriv;
            }
            public string DisplayName => string.IsNullOrEmpty(Usage) ? Name : Name + " " + Usage;
        }

        internal static readonly List<CommandEntry> Commands = new();

        internal static void Setup(TasePlugin plugin)
        {
            // Alias commands /tase and /tase_help (help menu)
            plugin.AddCommand("tase", "Show TASE help", plugin.CmdTaseHelp);
            plugin.AddCommand("tase_help", "Show TASE help", plugin.CmdTaseHelp);
            Commands.Add(new CommandEntry("tase / tase_help", "", "Show TASE help", TaseRole.None));

            plugin.AddCommand("tase_version", "Show TASE version", plugin.CmdTaseVersion);
            Commands.Add(new CommandEntry("tase_version", "", "Show plugin version & config", TaseRole.None));

            plugin.AddCommand("tase_me", "Show your title/privileges", plugin.CmdTaseMe);
            Commands.Add(new CommandEntry("tase_me", "", "Show your title/privileges", TaseRole.None));

            // Title management commands (GoD/Operator only)
            plugin.AddCommand("tase_titles_list", "List all titles", plugin.CmdTitlesList);
            Commands.Add(new CommandEntry("tase_titles_list", "", "List all current SteamID→Title mappings", TaseRole.Admin));

            plugin.AddCommand("tase_title_set", "Assign title", plugin.CmdTitleSet);
            Commands.Add(new CommandEntry("tase_title_set", "<steamid64> <title>", "Assign a title to a player", TaseRole.GoD, TasePrivilege.Operator));

            plugin.AddCommand("tase_title_clear", "Clear title", plugin.CmdTitleClear);
            Commands.Add(new CommandEntry("tase_title_clear", "<steamid64>", "Clear a player's title", TaseRole.GoD, TasePrivilege.Operator));

            plugin.AddCommand("tase_make_god", "Make player GoD", plugin.CmdMakeGod);
            Commands.Add(new CommandEntry("tase_make_god", "<steamid64>", "Grant 'GoD' title to a player", TaseRole.GoD, TasePrivilege.Operator));

            plugin.AddCommand("tase_make_admin", "Make player Admin", plugin.CmdMakeAdmin);
            Commands.Add(new CommandEntry("tase_make_admin", "<steamid64>", "Grant 'Admin' title to a player", TaseRole.GoD, TasePrivilege.Operator));

            // Moderation commands
            plugin.AddCommand("tase_jail", "Jail a player", plugin.CmdJail);
            Commands.Add(new CommandEntry("tase_jail", "<steamid64> [seconds]", "Jail a player for a duration", TaseRole.BlockBuster));

            plugin.AddCommand("tase_unjail", "Unjail a player", plugin.CmdUnjail);
            Commands.Add(new CommandEntry("tase_unjail", "<steamid64>", "Release a jailed player", TaseRole.BlockBuster));

            // Tools and UI commands
            plugin.AddCommand("grab", "Toggle Physics Gun", plugin.CmdGrab);
            Commands.Add(new CommandEntry("grab", "", "Toggle Physics Gun mode", TaseRole.GoD, TasePrivilege.UsePhysGun));

            plugin.AddCommand("tase_ui", "Open TASE UI", plugin.CmdTaseUi);
            Commands.Add(new CommandEntry("tase_ui", "", "Open TASE status UI (Panorama overlay)", TaseRole.None));

            plugin.AddCommand("bmtoggle", "Toggle BlockMaker UI", plugin.CmdBlockMakerToggle);
            Commands.Add(new CommandEntry("bmtoggle", "", "Toggle BlockMaker panel (map editor UI)", TaseRole.None, TasePrivilege.BuildBlocks));
        }
    }
}
