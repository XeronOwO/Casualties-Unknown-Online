using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The SOCIAL half of the world surface: the three things one player says to another rather than facts about the world — a text-chat line, a watch notice (the start
/// or end of spectating a member), and a transient location ping. Implemented by <see cref="WorldService"/>; <see cref="IWorldControl"/> inherits it, so every
/// existing reader keeps working and a reader that only needs the social surface can take this narrower interface instead of the whole world control.
/// </summary>
/// <remarks>
/// Each member is a report/relay pair with its own receive event, which is the shape the host routes them with: a guest reports, the host relays to the one member
/// the line is for, and the receiving side fires the event its own UI listens to. Splitting them out of <see cref="IWorldControl"/> keeps that routing rule in one
/// place instead of buried among the block, item and generation commands.
/// </remarks>
public interface IWorldSocialControl
{
	/// <summary>Guest: report a locally-authored text-chat line to the host.</summary>
	void SendChat(ChatMsg msg);

	/// <summary>Host only: fan a chat line out to every member except the author.</summary>
	void BroadcastChat(ulong excludeSteamId, ChatMsg msg);

	/// <summary>A chat line arrived: a guest's report on the host, a relay on the guests.</summary>
	void FireChatReceived(ulong sender, ChatMsg msg);

	event Action<ulong, ChatMsg>? ChatReceived;

	/// <summary>Watcher: report the start or the end of watching a member to the host.</summary>
	void SendSpectatorNotice(SpectatorNoticeMsg msg);

	/// <summary>Host only: relay a spectator notice to the watched member alone.</summary>
	void RelaySpectatorNotice(SpectatorNoticeMsg msg);

	/// <summary>A spectator notice arrived: a watcher's report on the host, a relay on the watched member.</summary>
	void FireSpectatorNoticeReceived(ulong sender, SpectatorNoticeMsg msg);

	event Action<ulong, SpectatorNoticeMsg>? SpectatorNoticeReceived;

	/// <summary>
	/// Report a locally-placed co-op location ping: guest → host as a report (the host adds it to its own UI and relays), host → broadcast to all synced members.
	/// The reporting player's client already added the marker locally. Ping is transient UI presentation — no authority/world state.
	/// </summary>
	void SendLocationPing(LocationPingMsg msg);

	/// <summary>Host only: relay an accepted location ping to the other members (source excluded — it already added the marker locally).</summary>
	void BroadcastLocationPing(ulong excludeSteamId, LocationPingMsg msg);

	void FireLocationPingReceived(ulong sender, LocationPingMsg msg);

	/// <summary>A location ping arrived — the receiver adds the transient marker locally (host: after a guest report; guest: the host's relay).</summary>
	event Action<ulong, LocationPingMsg>? LocationPingReceived;
}
