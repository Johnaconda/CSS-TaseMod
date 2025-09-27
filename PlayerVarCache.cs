// PlayerVarCache.cs — per-player resolved vars storage
using System.Collections.Generic;

namespace Tase
{
    public static class PlayerVarCache
    {
        private static readonly Dictionary<ulong, Dictionary<string, string>> _vars = new();

        public static void Set(ulong sid, Dictionary<string, string> vars)
            => _vars[sid] = new(vars);

        public static string Get(ulong sid, string key, string def = "")
            => _vars.TryGetValue(sid, out var m) && m.TryGetValue(key, out var v) ? v : def;
    }
}
