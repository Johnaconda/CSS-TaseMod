using System.Text.Json;

namespace Tase.Auth;

public enum TaseRole
{
    None = 0,
    BlockMaker = 10,
    BlockBuster = 20,
    Admin = 30,
    GoD = 40
}

internal sealed class RoleStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<ulong, TaseRole> _roles = new();

    internal RoleStore(string path) => _path = path;

    internal void Load()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            if (!File.Exists(_path))
            {
                SaveUnsafe();
                return;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<ulong, TaseRole>>(
                    File.ReadAllText(_path),
                    JsonOptions());

                _roles = parsed ?? new Dictionary<ulong, TaseRole>();
            }
            catch
            {
                // Do not silently destroy a malformed permissions file.
                _roles = new Dictionary<ulong, TaseRole>();
            }
        }
    }

    internal TaseRole Get(ulong steamId)
    {
        lock (_gate)
            return _roles.TryGetValue(steamId, out var role) ? role : TaseRole.None;
    }

    internal bool HasAtLeast(ulong steamId, TaseRole required) => Get(steamId) >= required;

    internal void Set(ulong steamId, TaseRole role)
    {
        lock (_gate)
        {
            if (role == TaseRole.None)
                _roles.Remove(steamId);
            else
                _roles[steamId] = role;

            SaveUnsafe();
        }
    }

    internal IReadOnlyDictionary<ulong, TaseRole> Snapshot()
    {
        lock (_gate)
            return new Dictionary<ulong, TaseRole>(_roles);
    }

    private void SaveUnsafe()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(_roles, JsonOptions());
        File.WriteAllText(_path, json);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true
    };
}
