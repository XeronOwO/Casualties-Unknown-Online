namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>A list index; negative counts from the end, as Minecraft's paths do.</summary>
public sealed record NbtPathIndex(int Index) : NbtPathSegment;
