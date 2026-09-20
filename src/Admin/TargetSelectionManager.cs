using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace Tase.Admin;

internal sealed class TargetSelectionManager
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, HashSet<ulong>> _selectedByActor = new();

    internal IReadOnlyCollection<ulong> Get(ulong actorSteamId)
    {
        lock (_gate)
        {
            return _selectedByActor.TryGetValue(actorSteamId, out var selected)
                ? selected.ToArray()
                : Array.Empty<ulong>();
        }
    }

    internal bool Contains(ulong actorSteamId, ulong targetSteamId)
    {
        lock (_gate)
            return _selectedByActor.TryGetValue(actorSteamId, out var selected)
                && selected.Contains(targetSteamId);
    }

    internal bool Toggle(ulong actorSteamId, ulong targetSteamId)
    {
        lock (_gate)
        {
            if (!_selectedByActor.TryGetValue(actorSteamId, out var selected))
            {
                selected = new HashSet<ulong>();
                _selectedByActor[actorSteamId] = selected;
            }

            if (!selected.Add(targetSteamId))
            {
                selected.Remove(targetSteamId);
                if (selected.Count == 0)
                    _selectedByActor.Remove(actorSteamId);
                return false;
            }

            return true;
        }
    }

    internal void ReplaceWith(ulong actorSteamId, ulong targetSteamId)
    {
        lock (_gate)
            _selectedByActor[actorSteamId] = new HashSet<ulong> { targetSteamId };
    }

    internal void Clear(ulong actorSteamId)
    {
        lock (_gate)
            _selectedByActor.Remove(actorSteamId);
    }

    internal void ForgetActor(ulong actorSteamId)
    {
        lock (_gate)
            _selectedByActor.Remove(actorSteamId);
    }

    internal void PruneDisconnectedPlayers()
    {
        var connected = Utilities.GetPlayers()
            .Where(player => player.IsValid && !player.IsBot)
            .Select(player => player.SteamID)
            .ToHashSet();

        lock (_gate)
        {
            foreach (var actor in _selectedByActor.Keys.ToArray())
            {
                if (!connected.Contains(actor))
                {
                    _selectedByActor.Remove(actor);
                    continue;
                }

                _selectedByActor[actor].RemoveWhere(target => !connected.Contains(target));
                if (_selectedByActor[actor].Count == 0)
                    _selectedByActor.Remove(actor);
            }
        }
    }

    internal IReadOnlyList<CCSPlayerController> ResolvePlayers(ulong actorSteamId)
    {
        var selected = Get(actorSteamId).ToHashSet();

        return Utilities.GetPlayers()
            .Where(player => player.IsValid && !player.IsBot && selected.Contains(player.SteamID))
            .ToList();
    }
}
