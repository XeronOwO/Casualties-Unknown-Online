using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The item-instance-id coordination: per-guest counter watermarks (guest →
/// host reports, host → guest grants on handshake completion — a
/// crashed-and-rejoined guest's counter restarts from zero and would reuse ids
/// the host's tables still hold) and the carried-inventory registration (the
/// guest's self-assigned starting-supply ids, registered in the transfer table
/// so its use/slot reports arbitrate normally). The registration is reported as
/// an ABSOLUTE set and repeated on the cadence of
/// <see cref="CarriedInventoryReportSchedule"/> — sync-coverage row I8: one
/// frame lost in the lazy-P2P swallow window must heal without a reconnect, and
/// ids the guest self-assigns later (a crafted product) must converge too. Ids
/// are (counter &lt;&lt; 32) | account-low — the space is per ACCOUNT
/// (<see cref="ItemInstanceIds"/>: the owner half is the account id's low 32 bits,
/// so it never equals the full SteamId), and so a per-space watermark is all the
/// coordination needed. A RESTORED world is the second source of those marks:
/// its item rows name the ids the previous process handed out, so a restore
/// seeds this table from them (<see cref="ResumeRestoredIds"/>) — the local side
/// included, whose own counter restarts with the process exactly like a
/// rejoining guest's.
///
/// <para>
/// ONE counter, in one namespace: the per-space watermark table above, which each
/// side resumes its own allocator from. A second, SYNTHESIZED counter used to live
/// here for the ids a host-side removal award minted; it is gone with the award,
/// because an applied item keeps its id across the round trip
/// (<c>ItemDomainModule</c>'s applied family) and nothing host-side mints an id of
/// its own any more. Neither table is cleared at a session boundary: the ids belong
/// to the world, not to a lobby.
/// </para>
/// Split out of ItemService when the 600-line gate demanded it.
/// </summary>
public sealed class ItemIdCoordinator : IDisposable, ISessionReset, ICarriedRegistrationFacts
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ItemArbitration _arbitration;
	private readonly ITimeSource _time;
	private readonly ILogger<ItemIdCoordinator> _log;

	/// <summary>Guest side: the carried-registration cadence (row I8) — the dense window that heals a swallowed registration, then the steady re-assertion.</summary>
	private readonly CarriedInventoryReportSchedule _carriedRegistration = new();

	/// <summary>Guest side: the largest carried set this session has actually registered — the baseline the inert-capture check compares an empty capture against.</summary>
	private int _registeredItems;

	/// <summary>Whether the inert-capture warning was already written for the current window.</summary>
	private bool _emptyCaptureWarned;

	/// <summary>
	/// Host side: the members whose carried-set statement this host has judged. It is
	/// the record behind <see cref="HasJudgedCarriedSet"/> — before a member appears
	/// here its items have no creation claim on this host, which is the swallowed-
	/// registration window; after it, that member's statement IS the claim for what it
	/// carries. Session-scoped: a new session's members state their sets again. A new
	/// RUN inside the session does not clear it (the transfer table's own clear is the
	/// run coordinator's, which knows nothing of this record); the member's statement is
	/// re-judged on the run's generation edge, and a report that arrives before it is
	/// refused and settled by that re-statement — one round trip, and no divergence,
	/// because the refused item is stated by the very report the refusal asks for.
	/// </summary>
	private readonly HashSet<ulong> _judgedCarriedSets = [];

	/// <summary>Host side: each ID SPACE's counter high-water mark (guest → host reports), keyed by
	/// <see cref="ItemInstanceIds.SpaceOf"/> — the value the ids themselves carry, never the full SteamId:
	/// a restored row knows only its id's owner half, and a table keyed by the 17-digit account id could
	/// not be addressed from it. Granted back on handshake completion — the member resumes from watermark + 1.</summary>
	private readonly Dictionary<ulong, ulong> _watermarks = [];

	/// <summary>This side's own resume point: the counter its allocator must resume from.
	/// Raised by the host's grant on join/reconnect (guest side) and by a restore that
	/// re-issued the archive's ids for the local SteamId — the same fact, so the adapter
	/// has one consumer for it (<c>ItemIdAllocator.SetWatermark</c>, raise-only).</summary>
	public event Action<ulong>? ItemIdWatermarkReceived;

	public ItemIdCoordinator(ISessionControl session, PacketSender sender, ItemArbitration arbitration, ITimeSource time, ILogger<ItemIdCoordinator> log)
	{
		_session = session;
		_sender = sender;
		_arbitration = arbitration;
		_time = time;
		_log = log;
		// The handshake-completion event — grant the watermark on join and on
		// reconnect (the presence table is stable across reconnects, so the
		// event re-fires; the recorded watermark is the resume point).
		session.MemberAdded += OnMemberAdded;
	}

	private void OnMemberAdded(ulong steamId)
	{
		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			var watermark = _watermarks.TryGetValue(ItemInstanceIds.SpaceOf(steamId), out var w) ? w : 0;
			GrantItemIdWatermark(steamId, watermark);
		}
	}

	/// <inheritdoc />
	public void Dispose() => _session.MemberAdded -= OnMemberAdded;

	/// <summary>Guest only: an item-instance id was allocated locally — report the counter high-water mark (the host grants it back on a reconnect).</summary>
	public void SendItemIdWatermark(ulong counter)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.ItemIdWatermark, new ItemIdWatermarkMsg { Counter = counter });
	}

	/// <summary>Host only: grant a member's id watermark (its allocations may resume from counter + 1 — 0 = it never allocated).</summary>
	public void GrantItemIdWatermark(ulong targetSteamId, ulong counter)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.ItemIdWatermark, new ItemIdWatermarkMsg { Counter = counter });
		_log.LogInformation("[IdWatermark] granted {Counter} to {Peer}.", counter, targetSteamId);
	}

	/// <summary>The id counter high-water mark arrived: host records it in the reporter's id space (the reconnect
	/// grant point), guest applies it (resume from counter + 1).</summary>
	public void FireItemIdWatermarkReceived(ulong sender, ulong counter)
	{
		if (_session.Role == SessionRole.Host)
		{
			var space = ItemInstanceIds.SpaceOf(sender);
			if (!_watermarks.TryGetValue(space, out var w) || counter > w)
			{
				_watermarks[space] = counter;
			}

			return;
		}

		ItemIdWatermarkReceived?.Invoke(counter);
	}

	/// <summary>
	/// Host/solo: a restore re-issued the archive's ids. Every restored row names its
	/// own instance id, and an id is (counter, id space) — so the archive states, per
	/// id space, how far the PREVIOUS process's counter had run, while this process's
	/// starts at zero (the local one included: the counters are per process). Seeding
	/// the watermarks from those ids is the rule the handshake edge already implements,
	/// applied to the restore edge, so both directions resume from one fact instead of
	/// two: the local side gets its resume point now (there is no grant to itself on the
	/// wire), and a member in the session is granted the raised value on the spot — the
	/// restored character it is about to receive carries those ids — while an absent
	/// member keeps it for its next handshake.
	/// <para>
	/// The caller hands in EVERY id the archive carries, from both of its carriers (the
	/// checkpoint's item rows and the character files: see <c>IRestoredItemIdSeed</c>),
	/// because this rule's input is "what the archive re-issued", not "what one carrier
	/// happened to hold".
	/// </para>
	/// <para>
	/// Raise-only, like every other writer of these counters: an id below a counter that
	/// is already recorded changes nothing, and the local event is idempotent because the
	/// allocator's own resume only ever raises.
	/// </para>
	/// </summary>
	internal void ResumeRestoredIds(IEnumerable<ulong> instanceIds)
	{
		var restored = new Dictionary<ulong, ulong>(); // id space → the highest counter the archive handed it
		foreach (var instanceId in instanceIds)
		{
			if (instanceId == 0)
			{
				continue; // an unbound item carries no id — the world-gen determinism covers it
			}

			var owner = ItemInstanceIds.OwnerOf(instanceId);
			var counter = ItemInstanceIds.CounterOf(instanceId);
			if (!restored.TryGetValue(owner, out var highest) || counter > highest)
			{
				restored[owner] = counter;
			}
		}

		foreach (var entry in restored)
		{
			var owner = entry.Key;
			var counter = entry.Value;
			if (owner == ItemInstanceIds.SpaceOf(_session.LocalSteamId))
			{
				// This side's own allocator: the counter it must resume from. Counter 0 is
				// a real resume point (the archive's first id), never "nothing to resume".
				_log.LogInformation("[IdWatermark] the restored world already handed this side ids up to counter {Counter}; resuming the local allocator above them.", counter);
				ItemIdWatermarkReceived?.Invoke(counter);
				continue;
			}

			var recorded = _watermarks.TryGetValue(owner, out var w) ? w : 0;
			if (counter <= recorded)
			{
				continue; // this member's own reports already reached higher — nothing to resume
			}

			_watermarks[owner] = counter;
			if (_session.Role == SessionRole.Host && _session.SessionActive && HandshakenMemberIn(owner) is { } member)
			{
				GrantItemIdWatermark(member, counter);
			}
		}
	}

	/// <summary>The member whose allocations live in this id space, when it is in the session right now — the target a raised watermark has to reach without waiting for a handshake that will not come.</summary>
	private ulong? HandshakenMemberIn(ulong space)
	{
		foreach (var member in _session.Members)
		{
			if (member.Handshaken && ItemInstanceIds.SpaceOf(member.SteamId) == space)
			{
				return member.SteamId;
			}
		}

		return null;
	}

	/// <summary>
	/// Session ended: the registration window is session-scoped — a new lobby's host records fresh grants
	/// from the new handshakes, and the guest's next world opens its own window.
	/// <para>
	/// The watermarks STAY. They are not a session fact: an id space's high-water mark belongs to the WORLD
	/// the ids live in, and that world outlives a lobby — a host may continue the same world in a new one.
	/// Forgetting them there is how an id gets re-issued: a member whose process restarted (its counter is
	/// back at zero) would be granted 0 and hand out ids the kernel and the scene already hold. Keeping a
	/// mark that is too high costs nothing (the resume is raise-only and the counter is 64 bits wide), so
	/// the table only ever grows within a process.
	/// </para>
	/// </summary>
	public void ResetSessionState()
	{
		_carriedRegistration.Reset();
		_registeredItems = 0;
		_emptyCaptureWarned = false;
		_judgedCarriedSets.Clear();
	}

	/// <summary>
	/// Guest only: the carried inventory with self-assigned ids — the host
	/// registers it in the guest's transfer table. The report is ABSOLUTE and
	/// REPEATABLE (row I8): every send spends one step of the registration
	/// window, and an empty capture spends a step too, so the pump can drive the
	/// cadence without capturing the body again on every frame.
	/// </summary>
	public void SendCarriedInventory(IReadOnlyList<CharacterItemMsg> items)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		var opened = _carriedRegistration.BurstRemaining == CarriedInventoryReportSchedule.BurstReports;
		_carriedRegistration.MarkReported(_time.NowMs);
		if (items.Count == 0)
		{
			if (_registeredItems > 0 && !_emptyCaptureWarned)
			{
				// A window that captures nothing while this session HAS registered a set spends
				// its whole budget sending nothing — the registration silently stops converging.
				// That is the inert-capture failure mode (a capture that skips whatever it has
				// already reported), so it is named instead of passing as "nothing to register".
				_emptyCaptureWarned = true;
				_log.LogWarning("[CarriedInventory] the registration captured nothing although this session already registered {Count} carried item(s) — if the guest still carries them, the capture has stopped seeing them and the re-report cannot converge.",
					_registeredItems);
			}
			else
			{
				_log.LogDebug("[CarriedInventory] nothing to register on this step of the window.");
			}

			if (!opened)
			{
				return;
			}

			// The window's FIRST step is a statement even when it is empty, and it has
			// to go out: the host judges "has this member stated what it carries" from
			// the report itself, and an empty set that is never stated leaves that
			// question open for the whole session — the window in which a report may
			// still materialize an item the member never claimed. (A body that cannot be
			// read is NOT this case: the reporter leaves the step unspent, so an empty
			// statement always means "enumerated, and it carried nothing".)
			_log.LogInformation("[CarriedInventory] opened the window with an EMPTY carried set — stating it, because the statement is what the host judges (and re-reporting every {Interval} ms while the window is open).",
				CarriedInventoryReportSchedule.BurstIntervalMs);
		}
		else if (opened)
		{
			_log.LogInformation("[CarriedInventory] reported {Count} carried item(s) with self-assigned ids — re-reporting every {Interval} ms while the window is open.",
				items.Count, CarriedInventoryReportSchedule.BurstIntervalMs);
		}
		else
		{
			_log.LogDebug("[CarriedInventory] re-reported {Count} carried item(s) with self-assigned ids ({Remaining} dense report(s) left in this window).",
				items.Count, _carriedRegistration.BurstRemaining);
		}

		_registeredItems = Math.Max(_registeredItems, items.Count);
		if (items.Count > 0)
		{
			// Only a statement that STATES something clears the inert-capture warning: an
			// empty opening statement would otherwise clear it and the next empty step
			// would name the same failure again, once per step.
			_emptyCaptureWarned = false;
		}

		_sender.Send(_session.HostSteamId, NetMsg.CarriedInventory, new CarriedInventoryMsg { Items = [.. items] });
	}

	/// <summary>Guest only: open the carried-registration window — the local generation finished, or the host granted the id watermark (the join/reconnect signal: a rejoined guest does not re-run its world generation, so that grant is its only registration edge).</summary>
	public void ArmCarriedInventoryRegistration(string reason)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_carriedRegistration.Arm(_time.NowMs);
		_emptyCaptureWarned = false; // a fresh window may name its own failure
		_log.LogDebug("[CarriedInventory] registration window opened: {Reason}.", reason);
	}

	/// <summary>
	/// Guest only: the next capture is due NOW, on the cadence the window already has.
	/// It is the settlement of a refused report (the host refused it because its own
	/// statement never carried the item), so the re-statement has to be immediate
	/// rather than up to a minute away — and it must not refill the dense budget, which
	/// would make a report that keeps being refused a stream of its own. A window that
	/// is not open is opened: nothing would report at all in that state.
	/// </summary>
	public void RequestCarriedInventoryReport(string reason)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_carriedRegistration.RequestNow(_time.NowMs);
		_log.LogDebug("[CarriedInventory] the next carried-set report is due now: {Reason}.", reason);
	}

	/// <summary>Guest only: whether the registration cadence wants a report now — the Game Adapter captures the current carried set and calls <see cref="SendCarriedInventory"/> when it does. Always false on the host and outside an active session.</summary>
	public bool IsCarriedInventoryRegistrationDue() =>
		_session.Role == SessionRole.Guest && _session.SessionActive && _carriedRegistration.IsDue(_time.NowMs);

	/// <summary>Host only: a guest's carried inventory with self-assigned ids arrived — register it in the guest's transfer table (its use/slot reports then arbitrate normally) and surface the fact-table entries (the host's clone of the guest renders the supplies immediately, and the snapshot divergence check sees the entries as already-known instead of a phantom pickup). Judging the statement is itself a fact the replication surface reads: from here on, this member's items have a creation claim on this host (<see cref="HasJudgedCarriedSet"/>).</summary>
	public void FireCarriedInventoryReceived(ulong sender, IReadOnlyList<CharacterItemMsg> items)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_arbitration.RegisterCarried(sender, items);
		_judgedCarriedSets.Add(sender);
		CarriedInventoryReceived?.Invoke(sender, items);
	}

	/// <inheritdoc />
	public bool HasJudgedCarriedSet(ulong member) => _judgedCarriedSets.Contains(member);

	/// <summary>Host side: a guest's self-assigned carried inventory arrived — the adapter merges it into the guest's fact table (clone render + snapshot divergence baseline).</summary>
	public event Action<ulong, IReadOnlyList<CharacterItemMsg>>? CarriedInventoryReceived;
}
