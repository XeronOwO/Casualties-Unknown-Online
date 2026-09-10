using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The character table the save layer binds to, without the domain service
/// behind it: the host slot plus one snapshot per SteamId, exactly the two
/// lookups <c>WorldSaveService</c> performs. Events and the reporting surface
/// are explicit no-ops — a save/restore test must not accidentally turn into a
/// character-sync test.
/// </summary>
internal sealed class FakeCharacterDataControl : ICharacterDataControl
{
	private readonly Dictionary<ulong, CharacterDataMsg> _saved = [];

	public CharacterDataMsg? HostData { get; private set; }

	public IReadOnlyDictionary<ulong, CharacterDataMsg> Saved => _saved;

	public void SaveCharacterData(ulong steamId, CharacterDataMsg msg) => _saved[steamId] = msg;

	public void SaveHostCharacterData(CharacterDataMsg msg) => HostData = msg;

	public CharacterDataMsg? GetSavedCharacter(ulong steamId) => _saved.TryGetValue(steamId, out var data) ? data : null;

	public CharacterDataMsg? GetHostCharacterData() => HostData;

	void ICharacterDataControl.SendSavedCharacter(ulong steamId) => throw new NotSupportedException("the save tests never send to a peer");

	void ICharacterDataControl.BroadcastHostCharacterData(CharacterDataMsg msg) => throw new NotSupportedException("the save tests never broadcast");

	void ICharacterDataControl.ReportCharacterData(CharacterDataMsg msg) => throw new NotSupportedException("the save tests never report");

	void ICharacterDataControl.ClearSavedCharacters() => _saved.Clear();

	void ICharacterDataControl.RelayCharacterData(ulong ownerSteamId, CharacterDataMsg msg) => throw new NotSupportedException("the save tests never relay");

	void ICharacterDataControl.FireCharacterDataReceived(ulong sender, CharacterDataMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.FireHostCharacterDataReceived(CharacterDataMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.ApplyEnemyBite(EnemyBiteMsg msg) => throw new NotSupportedException("the save tests never apply enemy effects");

	void ICharacterDataControl.ApplyEnemyLunge(EnemyLungeMsg msg) => throw new NotSupportedException("the save tests never apply enemy effects");

	void ICharacterDataControl.ApplyEnemyEffect(EnemyEffectMsg msg) => throw new NotSupportedException("the save tests never apply enemy effects");

	void ICharacterDataControl.ApplyLimbStateEvent(LimbStateEventMsg msg) => throw new NotSupportedException("the save tests never apply limb events");

	void ICharacterDataControl.FireLimbStateEventReceived(ulong sender, LimbStateEventMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.SendLimbStateEvent(LimbStateEventMsg msg) => throw new NotSupportedException("the save tests never send");

	void ICharacterDataControl.FireCharacterSoundReceived(ulong sender, CharacterSoundMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.SendCharacterSound(CharacterSoundMsg msg) => throw new NotSupportedException("the save tests never send");

	void ICharacterDataControl.FireCharacterAttackAnimReceived(ulong sender, CharacterAttackAnimMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.SendCharacterAttackAnim(CharacterAttackAnimMsg msg) => throw new NotSupportedException("the save tests never send");

	void ICharacterDataControl.FireCharacterLandingVisualReceived(ulong sender, CharacterLandingVisualMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.SendCharacterLandingVisual(CharacterLandingVisualMsg msg) => throw new NotSupportedException("the save tests never send");

	void ICharacterDataControl.FireCharacterRagdollReceived(ulong sender, CharacterRagdollMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.SendCharacterRagdoll(CharacterRagdollMsg msg) => throw new NotSupportedException("the save tests never send");

	event Action<ulong, CharacterDataMsg>? ICharacterDataControl.CharacterDataReceived
	{
		add { }
		remove { }
	}

	event Action<CharacterDataMsg>? ICharacterDataControl.HostCharacterDataReceived
	{
		add { }
		remove { }
	}

	event Action<ulong, LimbStateEventMsg>? ICharacterDataControl.LimbStateEventReceived
	{
		add { }
		remove { }
	}

	event Action<ulong, CharacterSoundMsg>? ICharacterDataControl.CharacterSoundReceived
	{
		add { }
		remove { }
	}

	event Action<ulong, CharacterAttackAnimMsg>? ICharacterDataControl.CharacterAttackAnimReceived
	{
		add { }
		remove { }
	}

	event Action<ulong, CharacterLandingVisualMsg>? ICharacterDataControl.CharacterLandingVisualReceived
	{
		add { }
		remove { }
	}

	event Action<ulong, CharacterRagdollMsg>? ICharacterDataControl.CharacterRagdollReceived
	{
		add { }
		remove { }
	}
}
