namespace Tase.BlockMaker;

internal enum BlockActionKind { Place, Delete }
internal sealed record BlockAction(BlockActionKind Kind, BlockDefinition Definition);
