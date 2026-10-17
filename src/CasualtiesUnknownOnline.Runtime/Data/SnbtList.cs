using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>An ordered list. Elements may be of different kinds (Minecraft 1.21.5 does the same in SNBT).</summary>
public sealed record SnbtList(IReadOnlyList<SnbtValue> Items) : SnbtValue
{
	public override string Kind => "a list";

	public static SnbtList Empty { get; } = new([]);
}
