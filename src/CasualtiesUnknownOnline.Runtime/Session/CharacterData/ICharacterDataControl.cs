using System;
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

	/// <summary>Host only: the host's own latest character snapshot (the cross-player interaction service's authority for host-owned carried items).</summary>
	CharacterDataMsg? GetHostCharacterData();

	/// <summary>Host only: record the host's own character snapshot (cross-player transfer result — the in-memory fact for host-owned items).</summary>
	void SaveHostCharacterData(CharacterDataMsg msg);

	/// <summary>Host only: broadcast the host's own character snapshot (the guests render the host's clone inventory from it).</summary>
	void BroadcastHostCharacterData(CharacterDataMsg msg);

	/// <summary>Guest side: report the local character snapshot to the host (1-2 Hz, driven by the Game Adapter).</summary>
	void ReportCharacterData(CharacterDataMsg msg);

	/// <summary>Host only: a NEW run started — the previous run's saved characters are void (a stale restore would wipe the new run's starting supplies).</summary>
	void ClearSavedCharacters();

	/// <summary>A character snapshot arrived (host: guest report; guest: host restore) — the Game Adapter applies/renders it.</summary>
	event Action<ulong, CharacterDataMsg>? CharacterDataReceived;

	/// <summary>Guest: the host's own snapshot arrived — render the host clone inventory.</summary>
	event Action<CharacterDataMsg>? HostCharacterDataReceived;

	/// <summary>A limb-latch event arrived — the Game Adapter applies the limb's terminal state to the owner's clone.</summary>
	event Action<ulong, LimbStateEventMsg>? LimbStateEventReceived;

	/// <summary>A character action sound arrived — the Game Adapter replays it on the owner's clone.</summary>
	event Action<ulong, CharacterSoundMsg>? CharacterSoundReceived;

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

	/// <summary>Host side: merge a limb-latch event's full limb + body terminal state into the owner's saved snapshot (the trigger rides the event, the snapshot is the fallback).</summary>
	void ApplyLimbStateEvent(LimbStateEventMsg msg);

	/// <summary>Surface an arrived limb-latch event (report or relay) for the Game Adapter to apply to the owner's clone.</summary>
	void FireLimbStateEventReceived(ulong sender, LimbStateEventMsg msg);

	/// <summary>Report/broadcast a limb-latch event: a guest reports its own limb latch to the host; the host broadcasts its own to every guest. Reliable — the trigger rides the event, never the snapshot.</summary>
	void SendLimbStateEvent(LimbStateEventMsg msg);

	/// <summary>Surface an arrived character-sound event (report or relay) for the Game Adapter to replay on the owner's clone.</summary>
	void FireCharacterSoundReceived(ulong sender, CharacterSoundMsg msg);

	/// <summary>Report/broadcast a character action sound: a guest reports its own sound to the host; the host broadcasts its own to every guest. One sound = one message.</summary>
	void SendCharacterSound(CharacterSoundMsg msg);
}
