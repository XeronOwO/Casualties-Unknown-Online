using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The character-sound wiring of the adapter (split from GameAdapter.cs at
/// the 600-line gate): the domain instance and the thin patch-bridge forward.
/// The constructor and the session bind/unbind live in the main partial.
/// </summary>
public sealed partial class GameAdapter
{
	private readonly CharacterSoundSync _characterSoundSync;

	void IPatchBridge.OnArmSwing() => _entities.MarkLocalAttackSwing();

	void IPatchBridge.OnCharacterSound(CharacterSoundKind kind, string clip, Vector2 pos, float volume, bool followOwner, bool twoDimensional, float recoilDegrees) =>
		_characterSoundSync.Report(kind, clip, pos, volume, followOwner, twoDimensional, recoilDegrees);
}
