using CasualtiesUnknownOnline.Runtime.Data;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>One parsed <c>/data remove</c>: the target and the path removed.</summary>
internal sealed record ItemDataRemoveRequest(ItemDataQuery Target, NbtPath Path);
