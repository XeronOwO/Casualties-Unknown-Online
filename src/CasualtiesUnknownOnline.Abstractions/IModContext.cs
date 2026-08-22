using System;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework surface a mod operates on — injected once via
/// <see cref="ICuoMod.Bind"/> at discovery, before Initialize. The session is
/// exposed as a SNAPSHOT (<see cref="Session"/> reflects the state at bind
/// time; the events below are the increments after that): the framework's own
/// session events fire before a mod is even discovered (host lobby creation,
/// a guest joining before the first frame), and the host side never fires
/// SessionActivated at all — the snapshot is the only reliable "current state"
/// a mod gets.
///
/// Event semantics (first-round contract): SessionActivated = the first
/// handshake completed (host side: never fired — read the snapshot);
/// PlayerJoined/PlayerLeft = a member's handshake completed / a member was
/// removed (NOT the in-world entity join — that is the entity domain's roster
/// broadcast); SessionEnded = the session tore down (host exit — a guest's
/// PlayerLeft for the host is NOT fired, only SessionEnded).
/// </summary>
public interface IModContext
{
	/// <summary>Mod-scoped logger — logs as [Mod:&lt;id&gt;].</summary>
	ILogger Logger { get; }

	/// <summary>The mod message channel (report/定向 semantics, star topology — no auto-relay).</summary>
	IModNetwork Network { get; }

	/// <summary>The session state at bind time (a snapshot, not a live view).</summary>
	ISessionInfo Session { get; }

	/// <summary>Host-authoritative commands — the handler always runs on the host's copy of the mod.</summary>
	IModCommands Commands { get; }

	/// <summary>
	/// Host-persistent per-mod state (opaque key/value bytes, scoped to this mod
	/// id). Writes require <see cref="ModPermission.WriteGameState"/> and the host
	/// role; see <see cref="IModState"/> for the full contract.
	/// </summary>
	IModState State { get; }

	/// <summary>
	/// Local mod UI windows (immediate-mode drawings on the local client). This
	/// surface is local-only and requires no permission — see <see cref="IModUi"/>.
	/// </summary>
	IModUi Ui { get; }

	/// <summary>The first member handshake completed (host side: never — see the snapshot).</summary>
	event Action? SessionActivated;

	/// <summary>A member's handshake completed — each member exactly once, including yourself.</summary>
	event Action<ulong>? PlayerJoined;

	/// <summary>A member was removed (host side only; the host itself never leaves).</summary>
	event Action<ulong>? PlayerLeft;

	/// <summary>The session ended (host exit / EndSession).</summary>
	event Action? SessionEnded;
}
