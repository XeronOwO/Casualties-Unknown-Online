using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Character-data domain: the SteamID-keyed character save/restore. Guests
/// report their snapshot (1 Hz, driven by the Game Adapter); the host keeps
/// the latest per SteamID IN MEMORY ONLY, and hands it back when the same
/// player reconnects. The world archive is the only persistent copy of a
/// character (decision 178): this table starts empty, is filled by the live
/// reports and by a restore's claim, and is cleared on session end / a new run.
/// The reconnect restore merges the item arbitration's transfer table (the
/// host's authoritative record of what the guest owns) over the guest's last
/// report — the host's STATE wins where they disagree, while the report keeps
/// stating PLACEMENT (see <see cref="TransferTableRestoreMerge"/>), and items the
/// guest never reported yet (a pickup moments before the disconnect) still
/// restore.
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
public sealed class CharacterDataStore : ICharacterDataControl, IDisposable, ISessionReset
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ILogger<CharacterDataStore> _log;
	private readonly IItemControl _items;
	private readonly PlayerKernelLimbProjection _playerLimbKernel;

	/// <summary>The applied item row's durability writer (see <see cref="AppliedItemConditionProjection"/>).</summary>
	private readonly AppliedItemConditionProjection _appliedItems;
	private readonly PlayerKernelRestoreProjection _playerKernelRestore;
	private readonly Dictionary<ulong, CharacterDataMsg> _savedCharacters; // host: last report per SteamID
	private CharacterDataMsg? _hostData; // host: the host's own latest character snapshot (same shape, broadcast to guests)

	/// <summary>Members whose latest report was refused for a non-finite physiological field; the warning is named once, not at 1 Hz.</summary>
	private readonly HashSet<ulong> _refusedReports = [];

	/// <summary>The field each refused member's report is refused for — carried back on the 10 Hz row so the refusal is answered.</summary>
	private readonly Dictionary<ulong, string> _refusalFields = [];

	/// <summary>The host clone's last successful advance per member (host-local, never on the wire).</summary>
	private readonly Dictionary<ulong, long> _modelAdvancingMs = [];

	/// <summary>The same freshness window the row pump uses; the reconciliation is skipped while the model is stalled.</summary>
	private const long HostModelFreshnessMs = 250;

	/// <summary>The event-merge rules over the table below (enemy terminal states, limb/injury, the heal reset result).</summary>
	private readonly CharacterEventMerges _eventMerges;

	public CharacterDataStore(ISessionControl session, PacketSender sender,
		ILogger<CharacterDataStore> log, IItemControl items,
		PlayerKernelLimbProjection playerLimbKernel,
		PlayerKernelRestoreProjection playerKernelRestore,
		AppliedItemConditionProjection appliedItems)
	{
		_session = session;
		_sender = sender;
		_log = log;
		_items = items;
		_playerLimbKernel = playerLimbKernel;
		_playerKernelRestore = playerKernelRestore;
		_appliedItems = appliedItems;
		_savedCharacters = [];
		_eventMerges = new CharacterEventMerges(FindSaved, playerLimbKernel, log);

		// The table is SESSION-scoped and starts EMPTY, deliberately: the world archive is
		// the only persistent copy of a character (decisions 162/164), and it feeds this
		// table at a restore through the claim (`WorldCharacterBinder`). The retired
		// `CasualtiesUnknownOnline.character-data.bin` store was a SECOND source of truth
		// for the same facts — the host's last report per SteamID, kept across process
		// starts — which is what decisions 162/164 set out to remove (S4 scope 6).
		//
		// Memory is further scoped to the session: the host session survives a guest
		// leaving (that reconnect restore still works), but a real session end (host exit /
		// lobby switch) clears the table.
		session.SessionEnded += ResetSessionState;
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

	/// <summary>
	/// Host side: keep the latest authoritative snapshot per SteamID. Callers are the host's own
	/// writers (event appliers, revives, restores); a member's plain report goes through
	/// <see cref="SaveReportedCharacterData"/> instead, which is the only path where the two
	/// continuous values the host owns are filtered.
	/// </summary>
	internal bool SaveCharacterData(ulong steamId, CharacterDataMsg msg)
	{
		if (RefuseNonFinite(steamId, msg, "authoritative write"))
		{
			return false;
		}

		_savedCharacters[steamId] = msg;
		_playerLimbKernel.SyncFromCharacterData(steamId, msg);
		_appliedItems.SyncFromCharacterData(steamId, msg);
		return true;
	}

	/// <summary>
	/// The authority path's finiteness gate: a non-finite value from an applier would freeze the
	/// member's model and row forever (the report path cannot repair the owned values it copies),
	/// so it is refused by name and the previous snapshot stays. The same gate protects the host's
	/// own broadcast snapshot.
	/// </summary>
	private bool RefuseNonFinite(ulong steamId, CharacterDataMsg msg, string path) =>
		CharacterDataFinite.Refuse(_log, steamId, msg, path);

	/// <summary>
	/// Host side: keep a member's 1 Hz report. The CONTINUOUS bleeding values are the host's, not
	/// the reporter's (the 2026-10-18 ruling): a plain report refreshes the model's inputs but
	/// never the three values the host owns, so a client that edits its own bleed, blood volume or
	/// total bleeding speed cannot state it. An event the host arbitrated writes them through
	/// <see cref="SaveCharacterData"/> instead, which is also what the host clone's simulation
	/// recognizes as authoritative.
	/// <para>
	/// A report with a non-finite physiological field is refused WHOLE and the previous snapshot is
	/// kept: the Game Adapter maps this snapshot onto the host's simulated clone, and a NaN there
	/// would poison every native rule and be pushed back to the owner. Returns false when refused,
	/// so the caller does not relay or render a snapshot that was never stored; at most one warning
	/// per member until a finite report arrives.
	/// </para>
	/// </summary>
	internal bool SaveReportedCharacterData(ulong steamId, CharacterDataMsg msg)
	{
		if (CharacterDataFinite.FirstNonFinite(msg) is { } field)
		{
			if (_refusedReports.Add(steamId))
			{
				_log.LogWarning("[Character] {Peer}'s report carries a non-finite {Field} — the whole report is refused and the host keeps the previous snapshot.",
					steamId, field);
			}

			_refusalFields[steamId] = field;
			return false;
		}

		if (_refusedReports.Remove(steamId))
		{
			_refusalFields.Remove(steamId);
			_log.LogInformation("[Character] {Peer}'s report is finite again; the host stores it.", steamId);
		}

		if (_savedCharacters.TryGetValue(steamId, out var previous))
		{
			// The comparison is BEFORE Preserve: the report's values are the client's claim, the
			// stored ones are the host model's latest write. This is the only detector of a wrong
			// model (ruling item 5); the host's value still stands. It is skipped while the model is
			// stalled (the clone failed, the world is paused): the client is then free-running and a
			// divergence line per report would be noise, not a model defect.
			if (IsHostModelAdvancing(steamId, Environment.TickCount, HostModelFreshnessMs))
			{
				var differences = VitalsReconciliation.Compare(previous, msg);
				if (differences.Count > 0)
				{
					_log.LogWarning("[Vitals] {Peer} divergence: {Differences}", steamId, string.Join("; ", differences));
				}
			}

			ContinuousVitals.Preserve(previous, msg);
		}

		// Preserve can copy a non-finite stored value into this report only if an earlier path
		// bypassed both guards; the inner save refuses it and THIS return must tell the caller, or
		// the handler would relay a snapshot the store never kept.
		return SaveCharacterData(steamId, msg);
	}

	/// <summary>Host side: merge an enemy bite's post-bite terminal state into the victim's saved snapshot immediately (the rules live in <see cref="CharacterEventMerges"/>).</summary>
	public void ApplyEnemyBite(EnemyBiteMsg msg) => _eventMerges.ApplyEnemyBite(msg);

	/// <summary>Host side: the <see cref="ApplyEnemyBite"/> mirror for a crystal-lunge terminal state.</summary>
	public void ApplyEnemyLunge(EnemyLungeMsg msg) => _eventMerges.ApplyEnemyLunge(msg);

	/// <summary>Host side: the <see cref="ApplyEnemyBite"/> mirror for an enemy-proximity side effect.</summary>
	public void ApplyEnemyEffect(EnemyEffectMsg msg) => _eventMerges.ApplyEnemyEffect(msg);

	/// <summary>
	/// Host only: a NEW run started (the host clicked start) — the saved
	/// character data belongs to the run that produced it: a fresh run starts
	/// fresh characters, the previous run's saves are void (a stale restore
	/// would wipe the new run's starting supplies — "started paradise, got the
	/// previous run's emergency light"). Same-run re-entries (death → menu →
	/// re-enter) find their save still in the table and restore normally.
	/// </summary>
	public void ClearSavedCharacters()
	{
		_savedCharacters.Clear();
		_hostData = null;
		_refusedReports.Clear();
		_refusalFields.Clear();
		_modelAdvancingMs.Clear();
		SavedCharactersCleared?.Invoke();
	}

	/// <summary>Session ended: the in-memory saves die with the session; the world archive is what a later start restores from.</summary>
	public void ResetSessionState()
	{
		_savedCharacters.Clear();
		_hostData = null;
		_refusedReports.Clear();
		_refusalFields.Clear();
		_modelAdvancingMs.Clear();
	}


	public void Dispose() => _session.SessionEnded -= ResetSessionState;

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
			// captured before a dedicated terminal event landed. A respawn snapshot
			// is already in the kernel: SaveCharacterData projected its alive/
			// conscious/limb/body facts there before this send (PlayerKernelLimbProjection),
			// so this projection confirms the revive instead of overwriting it.
			_playerKernelRestore.Apply(steamId, data);
			_sender.Send(steamId, NetMsg.CharacterData, data);
			_log.LogInformation("Sent saved character data to {Peer} ({Items} items).", steamId, data.Items.Count);
		}
	}

	/// <summary>
	/// Merge the host's authoritative ownership record (the transfer table — what the
	/// arbitration moved into the guest's hands, never overwritten by the guest's own
	/// reports) over the guest's last snapshot, and name both halves of the result: how
	/// many entries updated a node the snapshot already carried, and how many had to be
	/// appended because it carried the id nowhere. The rules live in
	/// <see cref="TransferTableRestoreMerge"/>.
	/// </summary>
	private void MergeTransferredItems(ulong steamId, CharacterDataMsg data)
	{
		var transferred = _items.GetTransferredItems(steamId);
		if (transferred.Count == 0)
		{
			return;
		}

		var outcome = TransferTableRestoreMerge.Apply(data, transferred);
		_log.LogInformation("Merged {Matched} transfer-table items onto the restore of {Peer} ({Appended} appended — the snapshot did not carry them; {Unplaced} unplaceable; {Total} items total).",
			outcome.Matched, steamId, outcome.Appended, outcome.Unplaced, data.Items.Count);
		if (outcome.Unplaced > 0)
		{
			_log.LogWarning("The restore of {Peer} cannot place {Unplaced} transfer-table item(s): the snapshot does not carry them anywhere and their entries state no body placement (a container content's slot is its parent's, and the table cannot state the parent — an item moved into a container after the last 1 Hz report). They are dropped by name, not silently.",
				steamId, outcome.Unplaced);
		}
	}

	/// <summary>Host: the latest report per SteamID (clone inventory rendering on body creation).</summary>
	public CharacterDataMsg? GetSavedCharacter(ulong steamId) =>
		_savedCharacters.TryGetValue(steamId, out var data) ? data : null;

	/// <summary>The saved-snapshot lookup the event-merge seams read; the table itself never leaves this class.</summary>
	private CharacterDataMsg? FindSaved(ulong steamId) =>
		_savedCharacters.TryGetValue(steamId, out var data) ? data : null;

	/// <summary>Host only: the field the current report refusal names, or null when the store is not refusing this member.</summary>
	public string? GetReportRefusal(ulong steamId) =>
		_refusedReports.Contains(steamId) && _refusalFields.TryGetValue(steamId, out var field) ? field : null;

	/// <summary>Host only: record that the member's simulated body just advanced; the row pump reads the freshness.</summary>
	public void MarkHostModelAdvancing(ulong steamId, long nowMs) => _modelAdvancingMs[steamId] = nowMs;

	/// <summary>Host only: whether the member's simulated body advanced within the given window.</summary>
	public bool IsHostModelAdvancing(ulong steamId, long nowMs, long maxAgeMs) =>
		_modelAdvancingMs.TryGetValue(steamId, out var atMs) && nowMs - atMs <= maxAgeMs;

	/// <summary>Host: the host's own latest character snapshot (cross-player interaction authority for host-owned items).</summary>
	public CharacterDataMsg? GetHostCharacterData() => _hostData;

	/// <summary>Host: record the host's own latest character snapshot (cross-player transfer result).</summary>
	public void SaveHostCharacterData(CharacterDataMsg msg)
	{
		if (RefuseNonFinite(_session.LocalSteamId, msg, "host snapshot"))
		{
			return;
		}

		_hostData = msg;
		_playerLimbKernel.SyncFromCharacterData(_session.LocalSteamId, msg);
		_appliedItems.SyncFromCharacterData(_session.LocalSteamId, msg);
		_log.LogInformation("Host character data updated ({Items} items).", msg.Items.Count);
	}

	/// <summary>Host only: broadcast the host's own snapshot — the guests render the host's clone inventory from it.</summary>
	public void BroadcastHostCharacterData(CharacterDataMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (RefuseNonFinite(_session.LocalSteamId, msg, "host broadcast"))
		{
			return;
		}

		_hostData = msg;
		_playerLimbKernel.SyncFromCharacterData(_session.LocalSteamId, msg);
		_appliedItems.SyncFromCharacterData(_session.LocalSteamId, msg);
		_session.Broadcast(NetMsg.HostCharacterData, msg);
	}

	/// <summary>
	/// A character snapshot arrived — host side: a guest's 1 Hz report (render its
	/// clone inventory); guest side: the host's reconnect restore (apply once the
	/// local body exists).
	/// </summary>
	public event Action<ulong, CharacterDataMsg>? CharacterDataReceived;

	/// <summary>Host: the saved-character table was cleared (new run / world restore) — dependent per-run state must die with it.</summary>
	public event Action? SavedCharactersCleared;

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

	/// <summary>Guest: the host-computed continuous-vitals row arrived — the Game Adapter applies it to its own body.</summary>
	public event Action<PlayerVitalsMsg>? PlayerVitalsReceived;


	/// <summary>Host side: merge a limb-latch event's full terminal state into the owner's saved snapshot immediately — a disconnect before the next 1 Hz report must still restore every changed limb + body field (the rules live in <see cref="CharacterEventMerges"/>).</summary>
	public bool ApplyLimbStateEvent(LimbStateEventMsg msg) => _eventMerges.ApplyLimbStateEvent(msg);

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
	/// Target client: offer the reset the host instructed, as the authority event. The ordinary
	/// character report that follows cannot carry it: <see cref="SaveReportedCharacterData"/>
	/// preserves the host's three continuous values, so the reset's blood volume and bleeds would be
	/// thrown away and the 10 Hz row would put the old values back within one tick. The transport's
	/// verdict is returned because the caller must re-offer a frame that was not sent — this event is
	/// the only carrier of the values it restores; false also covers this client no longer being a
	/// guest in an active session, and the caller drops the pending result with that session. A host
	/// reset needs no event; its own snapshot is already written authoritatively.
	/// </summary>
	public bool TrySendHealResetApplied(HealResetAppliedMsg msg)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return false;
		}

		// The event always speaks for THIS client's body, whatever a caller put in the payload: the
		// host's identity check then compares one structural fact with another.
		msg.OwnerSteamId = _session.LocalSteamId;
		return _sender.TrySend(_session.HostSteamId, NetMsg.HealResetApplied, msg, reliable: true);
	}

	/// <summary>
	/// Host side: merge the reset result a member's own client applied through the AUTHORITY path —
	/// unlike a plain report, its blood volume, total bleeding speed and per-limb bleed are kept as
	/// written. A no-snapshot, no-health or non-finite result is refused by name (the rules live in
	/// <see cref="CharacterEventMerges"/>), so the caller may treat false as "nothing was written".
	/// </summary>
	public bool ApplyHealResetEvent(HealResetAppliedMsg msg) => _eventMerges.ApplyHealResetEvent(msg);

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

	bool ICharacterDataControl.SaveReportedCharacterData(ulong steamId, CharacterDataMsg msg) => SaveReportedCharacterData(steamId, msg);
	void ICharacterDataControl.SendSavedCharacter(ulong steamId) => SendSavedCharacter(steamId);

	CharacterDataMsg? ICharacterDataControl.GetSavedCharacter(ulong steamId) => GetSavedCharacter(steamId);

	string? ICharacterDataControl.GetReportRefusal(ulong steamId) => GetReportRefusal(steamId);

	void ICharacterDataControl.MarkHostModelAdvancing(ulong steamId, long nowMs) => MarkHostModelAdvancing(steamId, nowMs);

	bool ICharacterDataControl.IsHostModelAdvancing(ulong steamId, long nowMs, long maxAgeMs) =>
		IsHostModelAdvancing(steamId, nowMs, maxAgeMs);

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

	bool ICharacterDataControl.ApplyLimbStateEvent(LimbStateEventMsg msg) => ApplyLimbStateEvent(msg);

	void ICharacterDataControl.FireLimbStateEventReceived(ulong sender, LimbStateEventMsg msg) =>
		LimbStateEventReceived?.Invoke(sender, msg);

	void ICharacterDataControl.SendLimbStateEvent(LimbStateEventMsg msg) => SendLimbStateEvent(msg);

	bool ICharacterDataControl.TrySendHealResetApplied(HealResetAppliedMsg msg) => TrySendHealResetApplied(msg);

	bool ICharacterDataControl.ApplyHealResetEvent(HealResetAppliedMsg msg) => ApplyHealResetEvent(msg);

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

	void ICharacterDataControl.FirePlayerVitalsReceived(PlayerVitalsMsg msg) =>
		PlayerVitalsReceived?.Invoke(msg);

	void ICharacterDataControl.SendCharacterRagdoll(CharacterRagdollMsg msg) => SendCharacterRagdoll(msg);

}
