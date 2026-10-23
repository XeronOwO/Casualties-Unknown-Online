using System;
using System.Collections.Generic;
using System.Threading;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The host side of the rule replication: it turns the member-admission and
/// config-change edges into one absolute announcement each, so the rules a member's
/// client reads are the host's own and not the member's config entries under the same
/// names.
///
/// <para>
/// It subscribes to both edges in its constructor and reacts to no others. A member's
/// handshake completing (<see cref="ISessionControl.MemberAdded"/>) is the first
/// announcement — the member is a member before it ever enters a world, and its member
/// list and Admin page are readable from the menu. A rule changing while the session
/// runs (the BepInEx config monitor: the config file, the Admin page and the console's
/// <c>/gamerule</c> command all write through it) is the second, so a mid-session flip
/// reaches every member at that moment rather than at the next world entry. The third
/// edge is the world-entry fan-out's own — the entry group and the 60 s repair group
/// call <see cref="Announce"/> like every other absolute table, which is what makes a
/// member whose admission announcement the lazy peer session swallowed converge.
/// </para>
///
/// <para>
/// A guest never announces: its own config entries change under the same names, and the
/// role guard is what keeps this process's local preferences from being broadcast as if
/// they were the session's rules.
/// </para>
///
/// <para>
/// The admission and world-entry edges send from the thread that raised them (the
/// receive path, i.e. the pump); the CHANGE edge is the only one whose thread is not
/// this service's to choose, so it hands the pump a flag instead of sending from the
/// callback (<see cref="ICuoService.Update"/>).
/// </para>
/// </summary>
public sealed class HostRulesAnnouncer : IHostRulesAnnouncement, ICuoService
{
	private readonly ISessionControl _session;
	private readonly IHostRules _hostRules;
	private readonly PacketSender _sender;
	private readonly ILogger<HostRulesAnnouncer> _log;
	private readonly IDisposable _hostRulesChanges;
	private readonly IDisposable _respawnChanges;

	/// <summary>
	/// A change seen by the monitor, waiting for the next frame. The callback that sets
	/// it belongs to whichever thread wrote the config entry — the Admin page and the
	/// console command write on the main thread, but BepInEx's own re-read of the file
	/// is its business, not this service's — while every send in this tree happens on
	/// the pump (<c>ICuoService.Update</c>). The flag is the whole hand-off: the
	/// callback never touches the transport, and the frame that consumes it reads the
	/// rules fresh, so a burst of writes costs one announcement per frame and the last
	/// one wins.
	/// </summary>
	private int _rulesChanged;

	public HostRulesAnnouncer(
		ISessionControl session,
		IHostRules hostRules,
		PacketSender sender,
		IOptionsMonitor<HostRulesOptions> hostRulesOptions,
		IOptionsMonitor<RespawnOptions> respawnOptions,
		ILogger<HostRulesAnnouncer> log)
	{
		_session = session;
		_hostRules = hostRules;
		_sender = sender;
		_log = log;

		// Both monitors, because the rule surface is composed from both (the host-only
		// flags and the respawn section): watching one would leave half the surface
		// changing silently. The change edge is the HOST's own config; a guest's entries
		// fire the same callback and are filtered by the role guard on the consuming
		// frame.
		_hostRulesChanges = hostRulesOptions.OnChange((_, _) => OnRulesChanged());
		_respawnChanges = respawnOptions.OnChange((_, _) => OnRulesChanged());
		_session.MemberAdded += OnMemberAdded;
	}

	/// <summary>The unbind half of the constructor's subscriptions — a process-lifetime singleton in production, and a disposed provider in the tests that build one per case.</summary>
	public void Dispose()
	{
		_session.MemberAdded -= OnMemberAdded;
		_hostRulesChanges.Dispose();
		_respawnChanges.Dispose();
	}

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	/// <summary>The frame the deferred change is announced on — the same thread every other send uses, whatever thread wrote the config entry.</summary>
	void ICuoService.Update()
	{
		if (Interlocked.Exchange(ref _rulesChanged, 0) == 0)
		{
			return;
		}

		AnnounceToEveryMember();
	}

	void ICuoService.Stop()
	{
	}

	public void Announce(ulong steamId)
	{
		if (_session.Role != SessionRole.Host)
		{
			_log.LogWarning("[Rules] announcement for {Peer} requested while this process is {Role} — ignored; only a host announces its rules.",
				steamId, _session.Role);
			return;
		}

		var msg = CurrentMessage();
		_sender.Send(steamId, NetMsg.HostRules, msg);
		_log.LogInformation("[Rules] announced the host rules to {Peer}: remoteAccess={RemoteAccess} memberCheatVerbs={MemberCheat} piggybackWeight={Weight:F2}.",
			steamId, RemoteBackpackAccessText.Format((RemoteBackpackAccess)msg.RemoteBackpackAccess), msg.AllowMemberCheatVerbs, msg.PiggybackWeightMultiplier);
	}

	/// <summary>The one place the live rule surface becomes a wire message: the set is read at the SEND point, so an announcement always carries the values in force when it left.</summary>
	private HostRulesMsg CurrentMessage() => HostRulesMsg.From(HostRuleSet.From(_hostRules));

	/// <summary>
	/// The admission edge. The member's handshake completed, so it stays a member for
	/// the rest of the session; this is the only announcement that reaches it before its
	/// first world entry, which is where its member list and its Admin page are read.
	/// </summary>
	private void OnMemberAdded(ulong steamId)
	{
		if (_session.Role != SessionRole.Host)
		{
			// The guest side raises the same event for the HOST member. Nothing to send.
			return;
		}

		Announce(steamId);
	}

	/// <summary>
	/// The change edge: a rule was written while the session runs. The callback only
	/// records it — the send belongs to the pump (<see cref="ICuoService.Update"/>), so
	/// the announcement leaves on the same thread every other send does whatever thread
	/// the config entry was written from.
	/// </summary>
	private void OnRulesChanged() => Interlocked.Exchange(ref _rulesChanged, 1);

	/// <summary>
	/// The frame that announces a change every member is told about at that moment rather
	/// than at the next world entry, because the rules govern gestures members make in
	/// the world they are already in. The set is read HERE, at the send point, so the
	/// announcement carries the values in force when it left rather than the ones the
	/// write replaced.
	/// </summary>
	private void AnnounceToEveryMember()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var members = new List<ulong>();
		foreach (var member in _session.Members)
		{
			members.Add(member.SteamId);
		}

		_log.LogInformation("[Rules] a host rule changed — announcing the set to {Count} member(s).", members.Count);
		foreach (var steamId in members)
		{
			Announce(steamId);
		}
	}
}
