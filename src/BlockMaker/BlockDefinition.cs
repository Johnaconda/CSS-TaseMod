namespace Tase.BlockMaker;

internal sealed class BlockDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public BlockShape Shape { get; set; }
    public string? ItemId { get; set; }
    public string? GeometryId { get; set; }
    public string Model { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }
    public float Roll { get; set; }
    public float Scale { get; set; } = 1f;
    public float SizeX { get; set; } = 64f;
    public float SizeY { get; set; } = 64f;
    public float SizeZ { get; set; } = 64f;
    public byte Red { get; set; } = 128;
    public byte Green { get; set; } = 128;
    public byte Blue { get; set; } = 128;
    public byte Alpha { get; set; } = 255;
    public BlockRenderMode RenderMode { get; set; } = BlockRenderMode.SolidWiremesh;
    public MediaBindingMode MediaBinding { get; set; } = MediaBindingMode.AutoByItemName;
    public ulong CreatedBySteamId { get; set; }
    public string? MediaId { get; set; }
}

internal sealed class BlockMapDocument
{
    public int SchemaVersion { get; set; } = 2;
    public string Map { get; set; } = "";
    public DateTime SavedUtc { get; set; } = DateTime.UtcNow;
    public List<BlockDefinition> Blocks { get; set; } = new();
}
