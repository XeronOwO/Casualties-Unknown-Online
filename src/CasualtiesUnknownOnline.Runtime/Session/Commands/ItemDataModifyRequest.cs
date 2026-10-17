using CasualtiesUnknownOnline.Runtime.Data;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>One parsed <c>/data modify</c>: the target, the path, the operation and the value the operation takes.</summary>
internal sealed record ItemDataModifyRequest(ItemDataQuery Target, NbtPath Path, string Operation, int Index, SnbtValue Value);
