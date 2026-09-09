using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>The trap-layout materialization dedup result: the entries that describe distinct entities, and how many entries were collapsed into an earlier one.</summary>
internal readonly struct TrapLayoutMaterializationResult
{
	internal IReadOnlyList<TrapLayoutEntryMsg> Entries { get; init; }

	internal int CollapsedCount { get; init; }
}
