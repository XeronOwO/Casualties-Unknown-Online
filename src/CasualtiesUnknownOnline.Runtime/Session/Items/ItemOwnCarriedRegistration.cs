using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The HOST's own carried-inventory registration: one per world entry, written by the
/// host itself because nobody else can state its authority.
///
/// <para>
/// A member's client states its carried set and the host registers it in that member's
/// transfer table (<see cref="ItemIdCoordinator"/>); a host has no such statement to
/// wait for, and before this its items were named only when a native act — a use, a
/// move, a drop — minted an id on first domain entry. Everything the player carries at
/// world entry (the starting supplies and the worn items) therefore had no row and no
/// instance id, so no data verb could name it and no member's record knew it.
/// </para>
///
/// <para>
/// Unlike the guest's statement this is a ONE-SHOT, not a cadence: the write is local,
/// so it cannot be lost on the wire, and a repeat would commit the same rows again for
/// every item. The debt is armed by the same edge the member's window opens on — the
/// local generation finished, or the session appeared on a world this client is already
/// in (a solo world turned lobby never sees a generation edge under that session) — and
/// is spent by the first capture that can read the body and is not taken mid-restore:
/// the adapter holds it while a local character restore is queued, because a respawn
/// layer places its fresh body one frame after the edge and its items two frames later,
/// so a capture taken then would state an empty set the host would never re-state. An
/// empty capture with no restore in flight spends the debt too: an empty set IS this
/// body's statement. The debt is session-scoped, so a session end clears it.
/// </para>
/// </summary>
internal sealed class ItemOwnCarriedRegistration(
	ISessionControl session,
	Action<ulong, CharacterItemMsg> writeCarried,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly Action<ulong, CharacterItemMsg> _writeCarried = writeCarried;
	private readonly ILogger _log = log;

	/// <summary>Whether a world entry still owes its registration.</summary>
	private bool _pending;

	/// <summary>A session ended: the owed registration belonged to that session's world record.</summary>
	internal void Reset() => _pending = false;

	/// <summary>Arm the one-shot: a world entry happened, or the session appeared on the world this client is already in.</summary>
	internal void Arm(string reason)
	{
		_pending = true;
		_log.LogDebug("[Items] the host's own carried registration is owed: {Reason}.", reason);
	}

	/// <summary>Whether the pump should capture the carried set now — only inside an active session, the only place a record exists.</summary>
	internal bool IsDue => _session.SessionActive && _pending;

	/// <summary>
	/// Spend the debt: every captured item takes the SAME write a native act of this host
	/// takes — the row is spawned when the record has none and updated when it does — so
	/// the item a host merely carries is addressable from the moment the world opens.
	/// </summary>
	internal void Register(IReadOnlyList<CharacterItemMsg> items)
	{
		if (!IsDue)
		{
			return; // nothing owed, or no session to record it in (a solo world keeps no record)
		}

		_pending = false;
		foreach (var item in items)
		{
			if (item.InstanceId == 0)
			{
				continue; // no id to state: the adapter mints one before it captures
			}

			_writeCarried(_session.LocalSteamId, item);
		}

		_log.LogInformation("[Items] the host's own carried inventory is registered: {Count} item(s) with ids and rows at world entry.", items.Count);
	}
}
