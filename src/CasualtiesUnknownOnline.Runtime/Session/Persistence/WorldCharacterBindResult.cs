using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// What binding a restored snapshot's characters onto the present peers produced:
/// the character the LOCAL player claims (or null — decision 162's "that player
/// joins as a new character"), the keys that were actually bound, and the stored
/// characters that were REFUSED (a loss, named in the restore's account).
///
/// The bound keys are the second half because a restore report has to be about what
/// HAPPENED: a stored file no present peer claims is not restored at all, so naming
/// its missing fields as damage would describe a degradation the player never gets —
/// and would hide the real one behind noise. The binder knows which keys it bound;
/// the caller that writes the report asks it instead of guessing from the archive.
///
/// A refusal is the other half of the same account, and it is deliberately NOT the
/// same as "nobody claims it": an ambiguous claim (two present players whose
/// transport-scoped keys collide) and a key-space mismatch (a Steam world opened over
/// IP-direct) both DROP a stored character that a present player may well have earned,
/// so they are losses and travel to the player instead of only to the log.
/// </summary>
/// <param name="LocalCharacter">The snapshot bound to this process's own peer, or null when the archive carries none this player claims.</param>
/// <param name="BoundPlayerKeys">The archive keys bound to a present peer (the local player's included) — the set a restore report may describe.</param>
/// <param name="ClaimRefusals">One line per stored character this restore refused to hand over, with its reason; empty = every stored character was either bound or belonged to an absent player.</param>
internal readonly record struct WorldCharacterBindResult(
	CharacterDataMsg? LocalCharacter,
	IReadOnlyList<string> BoundPlayerKeys,
	IReadOnlyList<string> ClaimRefusals);
