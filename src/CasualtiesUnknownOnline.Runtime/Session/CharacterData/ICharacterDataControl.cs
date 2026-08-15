using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The character-data surface packet handlers operate on — implemented by
/// CharacterDataStore. Handlers depend on this narrow interface instead of the
/// concrete service, which keeps the constructor graph acyclic (abstract
/// extraction, user rule).
/// </summary>
public interface ICharacterDataControl
{
	void SaveCharacterData(ulong steamId, CharacterDataMsg msg);

	void SendSavedCharacter(ulong steamId);

	/// <summary>Host: the latest report per SteamID (clone inventory rendering on body creation).</summary>
	CharacterDataMsg? GetSavedCharacter(ulong steamId);

	/// <summary>Host only: broadcast the host's own character snapshot (the guests render the host's clone inventory from it).</summary>
	void BroadcastHostCharacterData(CharacterDataMsg msg);

	/// <summary>Host only: relay a guest's report to the other guests (OwnerSteamId stamped, source excluded) — their clones of the reporter render its carried state.</summary>
	void RelayCharacterData(ulong ownerSteamId, CharacterDataMsg msg);

	void FireCharacterDataReceived(ulong sender, CharacterDataMsg msg);

	/// <summary>Guest: the host's own character snapshot arrived — render its clone inventory (never apply to the local body).</summary>
	void FireHostCharacterDataReceived(CharacterDataMsg msg);

	/// <summary>Host side: merge an enemy bite's post-bite terminal state into the victim's saved snapshot.</summary>
	void ApplyEnemyBite(EnemyBiteMsg msg);

	/// <summary>Host side: merge a crystal-lunge terminal state into the victim's saved snapshot.</summary>
	void ApplyEnemyLunge(EnemyLungeMsg msg);

	/// <summary>Host side: merge an enemy-proximity side effect terminal state into the victim's saved snapshot.</summary>
	void ApplyEnemyEffect(EnemyEffectMsg msg);
}
