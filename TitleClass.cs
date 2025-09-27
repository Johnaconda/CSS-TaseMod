// TitleClass.cs — title classes + privileges
using System;
using System.Collections.Generic;

namespace Tase
{
    [Flags]
    public enum TasePrivilege
    {
        None        = 0,
        BuildBlocks = 1 << 0,
        BustBlocks  = 1 << 1,
        UsePhysGun  = 1 << 2,
        JailPower   = 1 << 3,
        Operator    = 1 << 4, // elevated power inside our plugin
    }

    public class TitleClass
    {
        public string Name { get; set; } = string.Empty;
        public TaseRole MinRole { get; set; } = TaseRole.None;
        public TasePrivilege Privileges { get; set; } = TasePrivilege.None;

        // Plugin-scoped vars (string→string). Systems should read from here.
        public Dictionary<string, string> Vars { get; set; } = new();

        // Optional inheritance
        public List<string> Inherits { get; set; } = new();
    }
}
