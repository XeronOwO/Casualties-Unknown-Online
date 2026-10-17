namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>One named entry of a <see cref="SnbtCompound"/>; the order the operator wrote is the order the writer prints.</summary>
public readonly record struct SnbtEntry(string Key, SnbtValue Value);
