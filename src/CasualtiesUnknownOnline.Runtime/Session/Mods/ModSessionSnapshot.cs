using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// Builds the read-only bind-time session projection used both by
/// <see cref="ModService"/> (when a mod context is constructed) and by the
/// host-command executor (when a command handler needs the current session
/// facts). The host never fires SessionActivated and pre-discovery events are
/// lost, so this snapshot is the only reliable "current state" at bind time.
/// </summary>
internal static class ModSessionSnapshot
{
	public static ISessionInfo Capture(SessionService session) => new Snapshot(
		session.Role == SessionRole.Host,
		session.SessionActive,
		session.LocalSteamId,
		session.HostSteamId,
		[.. session.Members.Select(m => m.SteamId)]);

	private sealed class Snapshot(bool isHost, bool active, ulong local, ulong host, ulong[] members) : ISessionInfo
	{
		private readonly ulong[] _members = members;

		public bool IsHost { get; } = isHost;

		public bool SessionActive { get; } = active;

		public ulong LocalSteamId { get; } = local;

		public ulong HostSteamId { get; } = host;

		public IReadOnlyList<ulong> MemberSteamIds => _members;
	}
}
