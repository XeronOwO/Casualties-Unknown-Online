using CasualtiesUnknownOnline.Runtime.Data;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>One parsed <c>/data merge</c>: the target and the compound merged into the state's root.</summary>
internal sealed record ItemDataMergeRequest(ItemDataQuery Target, SnbtCompound State);
