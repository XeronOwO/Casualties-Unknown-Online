namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>The host's own worth for one judged item, or the reason its tables cannot compute one.</summary>
internal readonly record struct GiveItemValue(int Value, string? Refusal);
