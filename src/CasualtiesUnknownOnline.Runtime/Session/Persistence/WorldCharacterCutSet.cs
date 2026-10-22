using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// What one cut carries in <c>characters/</c>: the files it writes, the player each file belongs
/// to, and the players it could NOT carry. The ownership map exists because the archive keys a
/// character by its transport-scoped identity (decision 162) while the kernel and the item rows key
/// items by the transport peer id; the cut's one-placement reconcile needs both, and only this
/// binder knows the pair. The last half exists because two present IP-direct players sharing a
/// display name map to ONE key and only one file could ever be written under it — and a file under a
/// shared key is worse than no file, because a later restore could hand it to the wrong player. The
/// cut therefore carries NEITHER and NAMES them, the same verdict the claim side reaches at restore
/// time (decision 177), and the cut report renders it (§6: silent loss is forbidden).
/// </summary>
/// <param name="Characters">The character files this cut writes, one per distinct claimant key.</param>
/// <param name="Owners">The transport peer id each written file belongs to, keyed by the file's player key.</param>
/// <param name="NotCarried">One line per key the cut could not carry, with the players who share it.</param>
internal readonly record struct WorldCharacterCutSet(
	IReadOnlyList<SavedCharacter> Characters,
	IReadOnlyDictionary<string, ulong> Owners,
	IReadOnlyList<string> NotCarried);
