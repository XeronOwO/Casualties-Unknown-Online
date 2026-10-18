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

	private readonly Dictionary<ulong, string> _refusalFields = [];

	private readonly Dictionary<ulong, long> _modelAdvancingMs = [];

	public CharacterDataMsg? HostData { get; private set; }

	public IReadOnlyDictionary<ulong, CharacterDataMsg> Saved => _saved;

	public void SaveCharacterData(ulong steamId, CharacterDataMsg msg) => _saved[steamId] = msg;

	/// <summary>The real store's report rule, mirrored: a plain report cannot state the owned values, and a non-finite one is refused whole.</summary>
	public bool SaveReportedCharacterData(ulong steamId, CharacterDataMsg msg)
	{
		if (CharacterDataFinite.FirstNonFinite(msg) is { } field)
		{
			_refusalFields[steamId] = field;
			return false;
		}

		_refusalFields.Remove(steamId);
		if (_saved.TryGetValue(steamId, out var previous))
		{
			ContinuousVitals.Preserve(previous, msg);
		}

		_saved[steamId] = msg;
		return true;
	}

	public string? GetReportRefusal(ulong steamId) =>
		_refusalFields.TryGetValue(steamId, out var field) ? field : null;

	public void MarkHostModelAdvancing(ulong steamId, long nowMs) => _modelAdvancingMs[steamId] = nowMs;

	public bool IsHostModelAdvancing(ulong steamId, long nowMs, long maxAgeMs) =>
		_modelAdvancingMs.TryGetValue(steamId, out var atMs) && nowMs - atMs <= maxAgeMs;

	public void SaveHostCharacterData(CharacterDataMsg msg) => HostData = msg;

	public CharacterDataMsg? GetSavedCharacter(ulong steamId) => _saved.TryGetValue(steamId, out var data) ? data : null;

	public CharacterDataMsg? GetHostCharacterData() => HostData;

	void ICharacterDataControl.SendSavedCharacter(ulong steamId) => throw new NotSupportedException("the save tests never send to a peer");

	void ICharacterDataControl.BroadcastHostCharacterData(CharacterDataMsg msg) => throw new NotSupportedException("the save tests never broadcast");

	void ICharacterDataControl.ReportCharacterData(CharacterDataMsg msg) => throw new NotSupportedException("the save tests never report");

	void ICharacterDataControl.ClearSavedCharacters()
	{
		_saved.Clear();
		_refusalFields.Clear();
		_modelAdvancingMs.Clear();
		SavedCharactersCleared?.Invoke();
	}

	void ICharacterDataControl.RelayCharacterData(ulong ownerSteamId, CharacterDataMsg msg) => throw new NotSupportedException("the save tests never relay");

	void ICharacterDataControl.FireCharacterDataReceived(ulong sender, CharacterDataMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.FireHostCharacterDataReceived(CharacterDataMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.ApplyEnemyBite(EnemyBiteMsg msg) => throw new NotSupportedException("the save tests never apply enemy effects");

	void ICharacterDataControl.ApplyEnemyLunge(EnemyLungeMsg msg) => throw new NotSupportedException("the save tests never apply enemy effects");

	void ICharacterDataControl.ApplyEnemyEffect(EnemyEffectMsg msg) => throw new NotSupportedException("the save tests never apply enemy effects");

	bool ICharacterDataControl.ApplyLimbStateEvent(LimbStateEventMsg msg) => true;

	void ICharacterDataControl.FireLimbStateEventReceived(ulong sender, LimbStateEventMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.SendLimbStateEvent(LimbStateEventMsg msg) => throw new NotSupportedException("the save tests never send");

	/// <summary>How many applied-reset sends the next attempts answer with a false (transport-suppressed) verdict.</summary>
	internal int SuppressedResetSends { get; set; }

	/// <summary>The reset reports offered through the send seam, in order.</summary>
	internal List<HealResetAppliedMsg> AppliedResetSends { get; } = [];

	bool ICharacterDataControl.TrySendHealResetApplied(HealResetAppliedMsg msg)
	{
		AppliedResetSends.Add(msg);
		if (SuppressedResetSends > 0)
		{
			SuppressedResetSends--;
			return false;
		}

		return true;
	}

	bool ICharacterDataControl.ApplyHealResetEvent(HealResetAppliedMsg msg) => throw new NotSupportedException("the save tests never apply a reset");

	void ICharacterDataControl.FireCharacterSoundReceived(ulong sender, CharacterSoundMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.SendCharacterSound(CharacterSoundMsg msg) => throw new NotSupportedException("the save tests never send");

	void ICharacterDataControl.FireCharacterAttackAnimReceived(ulong sender, CharacterAttackAnimMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.SendCharacterAttackAnim(CharacterAttackAnimMsg msg) => throw new NotSupportedException("the save tests never send");

	void ICharacterDataControl.FireCharacterLandingVisualReceived(ulong sender, CharacterLandingVisualMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.SendCharacterLandingVisual(CharacterLandingVisualMsg msg) => throw new NotSupportedException("the save tests never send");

	void ICharacterDataControl.FireCharacterRagdollReceived(ulong sender, CharacterRagdollMsg msg) => throw new NotSupportedException("the save tests never fire");

	void ICharacterDataControl.SendCharacterRagdoll(CharacterRagdollMsg msg) => throw new NotSupportedException("the save tests never send");

	void ICharacterDataControl.FirePlayerVitalsReceived(PlayerVitalsMsg msg) => throw new NotSupportedException("the save tests never fire vitals");

	/// <summary>Test seam: simulate the store being cleared (new run / restore).</summary>
	internal void FireSavedCharactersCleared() => SavedCharactersCleared?.Invoke();

	public event Action? SavedCharactersCleared;

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

	event Action<PlayerVitalsMsg>? ICharacterDataControl.PlayerVitalsReceived
	{
		add { }
		remove { }
	}
}
