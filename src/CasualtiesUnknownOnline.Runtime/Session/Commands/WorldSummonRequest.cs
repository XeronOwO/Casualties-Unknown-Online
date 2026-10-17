using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One parsed <c>/summon</c> line, before anything is created: the prefab the operator named, whether it is an
/// item entity or a world entity, where it goes, and the state attached to an item.
///
/// <para>
/// <see cref="Position"/> is either one of the native command's words (<see cref="WorldSummonAnchor"/>, resolved
/// against the acting client's own scene later) or a literal point the operator typed. Both reach the same
/// creation call; only the resolution differs, and it belongs to the client whose pointer or body was named.
/// </para>
///
/// <para>
/// <see cref="StateText"/> is the CANONICAL form of the state — the text a member sends and the host re-parses
/// against its own capture of the item, so the parse that judges the values happens on the side that creates the
/// object. An empty text means "the prefab's own captured state"; it is always empty for an entity.
/// </para>
/// </summary>
internal sealed record WorldSummonRequest(string PrefabId, WorldSummonKind Kind, WorldSummonPosition Position, string StateText);
