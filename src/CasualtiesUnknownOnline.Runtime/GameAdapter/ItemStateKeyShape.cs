namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>One key of a compound shape: its name, its value's shape and a one-line description for the suggestion list.</summary>
public sealed record ItemStateKeyShape(string Name, ItemStateShape Shape, string? Description = null);
