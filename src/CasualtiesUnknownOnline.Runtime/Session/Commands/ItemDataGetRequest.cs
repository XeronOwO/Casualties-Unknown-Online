using CasualtiesUnknownOnline.Runtime.Data;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>One parsed <c>/data get</c>: the item target and the path into its state (the root path when none was typed).</summary>
internal sealed record ItemDataGetRequest(ItemDataQuery Target, NbtPath Path);
