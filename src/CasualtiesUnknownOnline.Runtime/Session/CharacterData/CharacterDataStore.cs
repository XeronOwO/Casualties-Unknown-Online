using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Character-data domain: the SteamID-keyed character save/restore. Guests
/// report their snapshot (1 Hz, driven by the Game Adapter); the host keeps
/// the latest per SteamID in memory and in the disk store, and hands it back
/// when the same player reconnects. The disk copy survives a host restart;
/// memory remains session-scoped (cleared on session end) while the file stays
/// until a NEW run voids it (`ClearSavedCharacters`).
/// The reconnect restore merges the item arbitration's transfer table (the
/// host's authoritative record of what the guest owns) over the guest's last
/// report — the host's data wins where they disagree, and items the guest
/// never reported yet (a pickup moments before the disconnect) still restore.
/// Terminal player facts (alive/conscious, limb latches, body-terminal
/// latches) come from the kernel players table, not from the snapshot's
/// terminal fields: the snapshot remains the authority for continuous
/// physiological values, skills, items, and position.
/// Not an ICuoService: it has no pump, it only reacts to reports and
/// handshakes. Reads role/session-active through <see cref="ISessionControl"/>
/// (resolved after the session is built) and the transfer table through
/// <see cref="IItemControl"/> — acyclic constructor graph (ItemService never
/// depends on this store), abstract extraction (user rule).
/// </summary>
public sealed class CharacterDataStore : ICharacterDataControl, IDisposable
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ILogger<CharacterDataStore> _log;
	private readonly IItemControl _items;
	private readonly CharacterDataFileStore _persistence;
	private readonly PlayerKernelLimbProjection _playerLimbKernel;
	private readonly PlayerKernelRestoreProjection _playerKernelRestore;
	private readonly Dictionary<ulong, CharacterDataMsg> _savedCharacters; // host: last report per SteamID
	private CharacterDataMsg? _hostData; // host: the host's own latest character snapshot (same shape, broadcast to guests)

	public CharacterDataStore(ISessionControl session, PacketSender sender,
		ILogger<CharacterDataStore> log, IItemControl items, CharacterDataFileStore persistence,
		PlayerKernelLimbProjection playerLimbKernel,
		PlayerKernelRestoreProjection playerKernelRestore)
	{
		_session = session;
		_sender = sender;
		_log = log;
		_items = items;
		_persistence = persistence;
		_playerLimbKernel = playerLimbKernel;
		_playerKernelRestore = playerKernelRestore;

		// Load the persisted table at construction — a host restart/continue-run
		// restores reconnecting guests from this file. A missing/disabled file is
		// an empty table; a corrupt/unknown-version file degrades to empty (the
		// store logs the reason). There is deliberately NO later lazy reload:
		// after a session end the old run's identity is unknown, so only a new
		// process start (the restart/continue-run path) may reload the disk copy.
		_persistence.TryLoad(out _savedCharacters);

		// Memory is SESSION-scoped: the host session survives a guest leaving
		// (that reconnect restore still works), but a real session end (host
		// exit / lobby switch) clears the active table. The disk copy is the
		// persistence layer and deliberately survives this reset.
		session.SessionEnded += OnSessionEnded;
	}

	/// <summary>
	/// Guest side: report the local character snapshot to the host (1-2 Hz,
	/// driven by the Game Adapter). The host keeps the latest per SteamID and
	/// hands it back when the same player reconnects.
	/// </summary>
	public void ReportCharacterData(CharacterDataMsg msg)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.CharacterData, msg);
	}

	/// <summary>Host side: keep the latest report per SteamID in memory and on disk.</summary>
	internal void SaveCharacterData(ulong steamId, CharacterDataMsg msg)
	{
		_savedCharacters[steamId] = msg;
		PersistTable();
		_playerLimbKernel.SyncFromCharacterData(steamId, msg);
	}

	/// <summary>
	/// Host side: merge an enemy bite's post-bite terminal state into the
	/// victim's saved snapshot immediately — a disconnect before the next 1 Hz
	/// report must still restore the bite (the dedicated event is the trigger;
	/// the snapshot is only the fallback).
	/// </summary>
	public void ApplyEnemyBite(EnemyBiteMsg msg)
	{
		if (_savedCharacters.TryGetValue(msg.VictimSteamId, out var data))
		{
			EnemyTerminalStateApplier.ApplyBite(data, msg);
			PersistTable();
		}
	}

	/// <summary>Host side: the EnemyBite mirror for a crystal-lunge terminal state.</summary>
	public void ApplyEnemyLunge(EnemyLungeMsg msg)
	{
		if (_savedCharacters.TryGetValue(msg.VictimSteamId, out var data))
		{
			EnemyTerminalStateApplier.ApplyLunge(data, msg);
			PersistTable();
		}
	}

	/// <summary>Host side: the EnemyBite mirror for an enemy-proximity side effect.</summary>
	public void ApplyEnemyEffect(EnemyEffectMsg msg)
	{
		if (_savedCharacters.TryGetValue(msg.VictimSteamId, out var data))
		{
			EnemyTerminalStateApplier.ApplyEffect(data, msg);
			PersistTable();
		}
	}

	/// <summary>
	/// Host only: a NEW run started (the host clicked start) — the saved
	/// character data belongs to the run that produced it: a fresh run starts
	/// fresh characters, the previous run's saves are void (a stale restore
	/// would wipe the new run's starting supplies — "started paradise, got the
	/// previous run's emergency light"). Same-run re-entries (death → menu →
	/// re-enter) find their save still in the table and restore normally. The
	/// disk copy is deleted too: a process restart must not resurrect it.
	/// </summary>
	public void ClearSavedCharacters()
	{
		_savedCharacters.Clear();
		_hostData = null;

		// Write the empty-table tombstone BEFORE deleting: if the delete fails,
		// the current file already reads as an empty new run — the old run can
		// never be resurrected by a later restart. If both writes fail, the
		// store logs the explicit degradation and this process stays empty.
		if (!_persistence.Save([]))
		{
			_log.LogWarning("Character-data disk tombstone write failed — if the old file survives, a restart may reload it.");
		}

		if (!_persistence.Delete())
		{
			_log.LogWarning("Character-data disk delete failed after the tombstone write — the file may remain, but it reads as empty.");
		}
	}

	/// <summary>Session ended: the in-memory saves die with the session; the disk copy survives for a host restart / continue-run.</summary>
	public void ResetForSessionEnd()
	{
		_savedCharacters.Clear();
		_hostData = null;
	}

	private void OnSessionEnded() => ResetForSessionEnd();

	public void Dispose() => _session.SessionEnded -= OnSessionEnded;

	/// <summary>
	/// Host side: hand the saved character data back to a reconnecting player,
	/// with the item arbitration's transfer table merged over it. Only while
	/// the host has a LIVE world: a menu handshake must never stage a previous
	/// run's restore for the next run (a fresh run clears the save only when
	/// the host clicks start, after that menu handshake already happened).
	/// </summary>
	internal void SendSavedCharacter(ulong steamId)
	{
		if (!_session.LocalInWorld)
		{
			_log.LogDebug("Not sending saved character data to {Peer}: the host is not in a world.", steamId);
			return;
		}

		if (_savedCharacters.TryGetValue(steamId, out var data))
		{
			MergeTransferredItems(steamId, data);
			// The kernel is the authority for terminal player facts; project it
			// over the saved snapshot so a reconnect restores the latest
			// alive/conscious/limb/body facts even if the last 1 Hz report was
			// captured before a dedicated terminal event landed.
			_playerKernelRestore.Apply(steamId, data);
			_sender.Send(steamId, NetMsg.CharacterData, data);
			_log.LogInformation("Sent saved character data to {Peer} ({Items} items).", steamId, data.Items.Count);
		}
	}

	/// <summary>Persist the full in-memory table after a verified mutation. A failed write keeps the in-memory save working for this process.</summary>
	private void PersistTable()
	{
		if (!_persistence.IsEnabled)
		{
			return;
		}

		if (!_persistence.Save(_savedCharacters))
		{
			_log.LogWarning("Character-data disk save failed — the in-memory save keeps working for this process.");
		}
	}

	/// <summary>
	/// Merge the host's authoritative ownership record (the transfer table —
	/// what the arbitration moved into the guest's hands, never overwritten by
	/// the guest's own reports) over the guest's last snapshot. An entry the
	/// snapshot already has is replaced by the authoritative state (the
	/// snapshot's slot is kept — a carried item's slot is its owner's local
	/// fact); an entry the snapshot lacks (a pickup moments before the
	/// disconnect) is appended.
	/// </summary>
	private void MergeTransferredItems(ulong steamId, CharacterDataMsg data)
	{
		var transferred = _items.GetTransferredItems(steamId);
		if (transferred.Count == 0)
		{
			return;
		}

		// Snapshot index: by instance id where present, else by definition id.
		var byId = new Dictionary<ulong, int>();
		var byDef = new Dictionary<string, int>();
		for (var i = 0; i < data.Items.Count; i++)
		{
			var item = data.Items[i];
			if (item.InstanceId != 0)
			{
				byId[item.InstanceId] = i;
			}
			else
			{
				byDef[item.ItemId] = i;
			}
		}

		var merged = 0;
		foreach (var entry in transferred)
		{
			var authoritative = entry.Item;
			var idx = authoritative.InstanceId != 0 && byId.TryGetValue(authoritative.InstanceId, out var byKey)
				? byKey
				: byDef.TryGetValue(authoritative.ItemId, out var byDefinition) ? byDefinition : -1;
			if (idx >= 0)
			{
				authoritative.SlotIndex = data.Items[idx].SlotIndex; // the snapshot's slot is the owner's local fact
				data.Items[idx] = authoritative;
			}
			else
			{
				data.Items.Add(authoritative);
			}

			merged++;
		}

		_log.LogInformation("Merged {Merged} transfer-table items into the restore of {Peer} ({Total} items total).",
			merged, steamId, data.Items.Count);
	}

	/// <summary>Host: the latest report per SteamID (clone inventory rendering on body creation).</summary>
	public CharacterDataMsg? GetSavedCharacter(ulong steamId) =>
		_savedCharacters.TryGetValue(steamId, out var data) ? data : null;

	/// <summary>Host: the host's own latest character snapshot (cross-player interaction authority for host-owned items).</summary>
	public CharacterDataMsg? GetHostCharacterData() => _hostData;

	/// <summary>Host: record the host's own latest character snapshot (cross-player transfer result).</summary>
	public void SaveHostCharacterData(CharacterDataMsg msg)
	{
		_hostData = msg;
		_playerLimbKernel.SyncFromCharacterData(_session.LocalSteamId, msg);
		_log.LogInformation("Host character data updated ({Items} items).", msg.Items.Count);
	}

	/// <summary>Host only: broadcast the host's own snapshot — the guests render the host's clone inventory from it.</summary>
	public void BroadcastHostCharacterData(CharacterDataMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_hostData = msg;
		_playerLimbKernel.SyncFromCharacterData(_session.LocalSteamId, msg);
		_session.Broadcast(NetMsg.HostCharacterData, msg);
	}

	/// <summary>
	/// A character snapshot arrived — host side: a guest's 1 Hz report (render its
	/// clone inventory); guest side: the host's reconnect restore (apply once the
	/// local body exists).
	/// </summary>
	public event Action<ulong, CharacterDataMsg>? CharacterDataReceived;

	/// <summary>Guest: the host's own 1 Hz snapshot arrived — render its clone inventory (never apply).</summary>
	public event Action<CharacterDataMsg>? HostCharacterDataReceived;

	/// <summary>A limb-latch event arrived (report or relay) — the Game Adapter applies the limb's terminal state to the owner's clone.</summary>
	public event Action<ulong, LimbStateEventMsg>? LimbStateEventReceived;

	/// <summary>A character action sound arrived (report or relay) — the Game Adapter replays it on the owner's clone.</summary>
	public event Action<ulong, CharacterSoundMsg>? CharacterSoundReceived;

	/// <summary>A character attack-animation event arrived (report or relay) — the Game Adapter replays it on the owner's clone.</summary>
	public event Action<ulong, CharacterAttackAnimMsg>? CharacterAttackAnimReceived;

	/// <summary>A character landing-visual event arrived (report or relay) — the Game Adapter replays the Grounded clip/dust on the owner's clone.</summary>
	public event Action<ulong, CharacterLandingVisualMsg>? CharacterLandingVisualReceived;

	/// <summary>A character ragdoll-toggle event arrived (report or relay) — the Game Adapter replays the lying pose on the owner's clone.</summary>
	public event Action<ulong, CharacterRagdollMsg>? CharacterRagdollReceived;


	/// <summary>Host side: merge a limb-latch event's full terminal state into the owner's saved snapshot immediately — a disconnect before the next 1 Hz report must still restore every changed limb + body field (the dedicated event is the trigger; the snapshot is only the fallback).</summary>
	public void ApplyLimbStateEvent(LimbStateEventMsg msg)
	{
		if (_savedCharacters.TryGetValue(msg.OwnerSteamId, out var data))
		{
			EnemyTerminalStateApplier.ApplyLimbState(data, msg);
			PersistTable();
		}

		_playerLimbKernel.SyncFromLimbEvent(msg);
	}

	/// <summary>
	/// Report/broadcast a limb-latch event: a guest reports its own limb to the
	/// host; the host broadcasts its own to every handshaken guest (its body is
	/// already damaged locally). Reliable — a lost event self-heals on the next
	/// 1 Hz character snapshot, but the trigger rides the event, never the
	/// snapshot (mirror of EnemySyncService.SendEnemyBite).
	/// </summary>
	public void SendLimbStateEvent(LimbStateEventMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_playerLimbKernel.SyncFromLimbEvent(msg);
		}

		if (_session.Role == SessionRole.Host)
		{
			_sender.SendToAll(
				_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
				NetMsg.LimbStateEvent, msg, reliable: true);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.LimbStateEvent, msg);
		}
	}

	/// <summary>
	/// Report/broadcast a character action sound: a guest reports its own sound
	/// to the host; the host broadcasts its own to every handshaken guest (it
	/// already heard the sound locally). Reliable — one sound = one message,
	/// the presentation trigger never rides the snapshot stream.
	/// </summary>
	public void SendCharacterSound(CharacterSoundMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_sender.SendToAll(
				_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
				NetMsg.CharacterSound, msg, reliable: true);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.CharacterSound, msg);
		}
	}

	/// <summary>
	/// Report/broadcast a character attack animation: a guest reports its own
	/// visual to the host; the host broadcasts its own to every handshaken
	/// guest. Reliable — one animation = one message, the visual trigger never
	/// rides the snapshot stream.
	/// </summary>
	public void SendCharacterAttackAnim(CharacterAttackAnimMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_sender.SendToAll(
				_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
				NetMsg.CharacterAttackAnim, msg, reliable: true);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.CharacterAttackAnim, msg);
		}
	}

	/// <summary>
	/// Report/broadcast a character landing visual: a guest reports its own
	/// landing to the host; the host broadcasts its own to every handshaken
	/// guest. Reliable — one landing = one message, the visual trigger never
	/// rides the snapshot stream.
	/// </summary>
	public void SendCharacterLandingVisual(CharacterLandingVisualMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_sender.SendToAll(
				_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
				NetMsg.CharacterLandingVisual, msg, reliable: true);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.CharacterLandingVisual, msg);
		}
	}

	/// <summary>
	/// Report/broadcast a character ragdoll toggle: a guest reports its own
	/// collapse to the host; the host broadcasts its own to every handshaken
	/// guest. Reliable — one collapse = one message, the presentation trigger
	/// never rides the snapshot stream.
	/// </summary>
	public void SendCharacterRagdoll(CharacterRagdollMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_sender.SendToAll(
				_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
				NetMsg.CharacterRagdoll, msg, reliable: true);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.CharacterRagdoll, msg);
		}
	}


	// ---- ICharacterDataControl (the packet handlers' control surface) ----

	void ICharacterDataControl.SaveCharacterData(ulong steamId, CharacterDataMsg msg) => SaveCharacterData(steamId, msg);

	void ICharacterDataControl.SendSavedCharacter(ulong steamId) => SendSavedCharacter(steamId);

	CharacterDataMsg? ICharacterDataControl.GetSavedCharacter(ulong steamId) => GetSavedCharacter(steamId);

	CharacterDataMsg? ICharacterDataControl.GetHostCharacterData() => GetHostCharacterData();

	void ICharacterDataControl.SaveHostCharacterData(CharacterDataMsg msg) => SaveHostCharacterData(msg);

	void ICharacterDataControl.BroadcastHostCharacterData(CharacterDataMsg msg) => BroadcastHostCharacterData(msg);

	void ICharacterDataControl.RelayCharacterData(ulong ownerSteamId, CharacterDataMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		// Stamping the shared instance is safe: it is the SAVED copy (the
		// handler saved the same reference), and the only later senders of it
		// are the owner's own restore — the receiver's owner check restores
		// exactly for OwnerSteamId == itself.
		msg.OwnerSteamId = ownerSteamId;
		_session.BroadcastExcept(ownerSteamId, NetMsg.CharacterData, msg);
		_log.LogDebug("Relayed character data of {Owner} to the other guests ({Items} items).", ownerSteamId, msg.Items.Count);
	}

	void ICharacterDataControl.FireCharacterDataReceived(ulong sender, CharacterDataMsg msg) =>
		CharacterDataReceived?.Invoke(sender, msg);

	void ICharacterDataControl.FireHostCharacterDataReceived(CharacterDataMsg msg) =>
		HostCharacterDataReceived?.Invoke(msg);

	void ICharacterDataControl.ApplyEnemyBite(EnemyBiteMsg msg) => ApplyEnemyBite(msg);

	void ICharacterDataControl.ApplyEnemyLunge(EnemyLungeMsg msg) => ApplyEnemyLunge(msg);

	void ICharacterDataControl.ApplyEnemyEffect(EnemyEffectMsg msg) => ApplyEnemyEffect(msg);

	void ICharacterDataControl.ApplyLimbStateEvent(LimbStateEventMsg msg) => ApplyLimbStateEvent(msg);

	void ICharacterDataControl.FireLimbStateEventReceived(ulong sender, LimbStateEventMsg msg) =>
		LimbStateEventReceived?.Invoke(sender, msg);

	void ICharacterDataControl.SendLimbStateEvent(LimbStateEventMsg msg) => SendLimbStateEvent(msg);

	void ICharacterDataControl.FireCharacterSoundReceived(ulong sender, CharacterSoundMsg msg) =>
		CharacterSoundReceived?.Invoke(sender, msg);

	void ICharacterDataControl.SendCharacterSound(CharacterSoundMsg msg) => SendCharacterSound(msg);

	void ICharacterDataControl.FireCharacterAttackAnimReceived(ulong sender, CharacterAttackAnimMsg msg) =>
		CharacterAttackAnimReceived?.Invoke(sender, msg);

	void ICharacterDataControl.SendCharacterAttackAnim(CharacterAttackAnimMsg msg) => SendCharacterAttackAnim(msg);

	void ICharacterDataControl.FireCharacterLandingVisualReceived(ulong sender, CharacterLandingVisualMsg msg) =>
		CharacterLandingVisualReceived?.Invoke(sender, msg);

	void ICharacterDataControl.SendCharacterLandingVisual(CharacterLandingVisualMsg msg) => SendCharacterLandingVisual(msg);

	void ICharacterDataControl.FireCharacterRagdollReceived(ulong sender, CharacterRagdollMsg msg) =>
		CharacterRagdollReceived?.Invoke(sender, msg);

	void ICharacterDataControl.SendCharacterRagdoll(CharacterRagdollMsg msg) => SendCharacterRagdoll(msg);

}
