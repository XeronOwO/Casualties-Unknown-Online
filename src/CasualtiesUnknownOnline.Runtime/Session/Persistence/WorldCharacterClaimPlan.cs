using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The claims a restore's stored characters resolve to against the PRESENT peers, decided
/// before the kernel write: a character no present peer claims is carried by no body, so its
/// item rows must not enter this session's world (decision 162 — that player joins as a NEW
/// character, and the file waits in the archive for a later claim).
///
/// It is data so the caller can act on the rows and then bind the claimed characters without
/// resolving the claim twice.
/// </summary>
/// <param name="Space">The key space the stored characters were written in (the live one when the archive does not say).</param>
/// <param name="Claimed">The stored characters a present peer claims, with the peer each goes to.</param>
/// <param name="Unclaimed">The stored characters no present peer claims — kept in the archive, never restored now.</param>
/// <param name="Refusals">One line per stored character this restore refused to hand over (ambiguous claim, key-space mismatch); empty when every stored character was claimed or merely unclaimed.</param>
internal sealed record WorldCharacterClaimPlan(
	PlayerKeySpace Space,
	IReadOnlyList<(SavedCharacter Character, ulong PeerId)> Claimed,
	IReadOnlyList<SavedCharacter> Unclaimed,
	IReadOnlyList<string> Refusals);
