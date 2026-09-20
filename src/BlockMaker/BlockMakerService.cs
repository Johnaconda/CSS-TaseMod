namespace Tase.BlockMaker;

internal enum BlockShape
{
    Box,
    Slab,
    Pillar,
    Wedge,
    InvertedWedge,
    InnerCorner,
    OuterCorner
}

internal sealed class BlockMakerSession
{
    internal BlockShape Shape { get; set; } = BlockShape.Box;
    internal float SizeX { get; set; } = 64f;
    internal float SizeY { get; set; } = 64f;
    internal float SizeZ { get; set; } = 64f;
    internal float GridSnap { get; set; } = 16f;
    internal float RotationStep { get; set; } = 15f;
    internal byte Red { get; set; } = 128;
    internal byte Green { get; set; } = 128;
    internal byte Blue { get; set; } = 128;
    internal byte Alpha { get; set; } = 255;
    internal bool PreviewEnabled { get; set; } = true;
}

internal sealed class BlockMakerService
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, BlockMakerSession> _sessions = new();

    internal BlockMakerSession GetSession(ulong steamId)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(steamId, out var session))
            {
                session = new BlockMakerSession();
                _sessions[steamId] = session;
            }

            return session;
        }
    }

    internal void ResetSession(ulong steamId)
    {
        lock (_gate)
            _sessions[steamId] = new BlockMakerSession();
    }

    internal void Forget(ulong steamId)
    {
        lock (_gate)
            _sessions.Remove(steamId);
    }
}
