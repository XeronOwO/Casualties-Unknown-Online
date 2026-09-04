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

	/// <summary>Local in-game console commands — the handler runs on the local process, with no wire relay.</summary>
	IModConsoleCommands ConsoleCommands { get; }

	/// <summary>
	/// Host-persistent per-mod state (opaque key/value bytes, scoped to this mod
	/// id). Writes require <see cref="ModPermission.WriteGameState"/> and the host
	/// role; see <see cref="IModState"/> for the full contract.
	/// </summary>
	IModState State { get; }

	/// <summary>
	/// Per-mod runtime data — ephemeral, process-local, scope-declared values.
	/// Local-only data never crosses the wire; shared and host-authoritative
	/// data is transport-explicit through <see cref="IModNetwork"/> /
	/// <see cref="IModCommands"/> rather than an automatic snapshot protocol.
	/// See <see cref="IModData"/> for the full contract.
	/// </summary>
	IModData Data { get; }

	/// <summary>
	/// Per-mod runtime status values — the typed phase-1 counterpart to static
	/// <see cref="ModStatusDefinition"/> content. It is keyed by player and
	/// optional limb slot and uses the same scope rules as <see cref="IModData"/>.
	/// See <see cref="IModStatusRuntime"/> for the full contract.
	/// </summary>
	IModStatusRuntime StatusRuntime { get; }

	/// <summary>
	/// Typed status wire helpers — phase 2. It publishes committed shared
	/// status values over the existing <see cref="IModNetwork"/> channel as
	/// <see cref="ModStatusUpdate"/> frames and applies host-originated frames
	/// to the local guest mirror. See <see cref="IModStatusTransport"/> for the
	/// full contract and authority rules.
	/// </summary>
	IModStatusTransport StatusTransport { get; }

	/// <summary>
	/// Per-mod local moodle-presentation resolvers. A mod can register a
	/// resolver per runtime status id and route active body/limb statuses to a
	/// static <see cref="ModMoodleDefinition"/> from its own opaque payload.
	/// This is local-only presentation; it never exposes game/Unity types and
	/// adds no wire surface. See <see cref="IModMoodleRuntime"/>.
	/// </summary>
	IModMoodleRuntime MoodleRuntime { get; }

	/// <summary>
	/// Per-mod runtime building hooks. A mod can register prefab and instance
	/// hooks that return component type names for the Game Adapter to attach to
	/// its custom building template/instances. This is the CUO-safe replacement
	/// for CUCoreLib's GameObject callbacks: it never exposes Unity/game types
	/// and adds no wire surface. See <see cref="IModBuildingRuntime"/>.
	/// </summary>
	IModBuildingRuntime BuildingRuntime { get; }

	/// <summary>
	/// Local mod UI windows (immediate-mode drawings on the local client). This
	/// surface is local-only and requires no permission — see <see cref="IModUi"/>.
	/// </summary>
	IModUi Ui { get; }

	/// <summary>
	/// Mod content registration (opaque content definitions scoped to this mod
	/// id). Registration requires <see cref="ModPermission.RegisterContent"/> —
	/// see <see cref="IModContent"/> for the full contract.
	/// </summary>
	IModContent Content { get; }

	/// <summary>
	/// Read-only framework-wide content ownership lookup. It resolves which mod
	/// registered a given content kind + id, without exposing Runtime internals
	/// or interpreting payloads. See <see cref="IModContentOwnerQuery"/>.
	/// </summary>
	IModContentOwnerQuery ContentOwners { get; }

	/// <summary>
	/// The read-only game-state projection (currently the latest known player
	/// character state). Reading requires <see cref="ModPermission.ReadGameState"/>
	/// — see <see cref="IModGameState"/> for the full contract.
	/// </summary>
	IModGameState GameState { get; }

	/// <summary>
	/// The world entity-spawn surface. Spawning requires
	/// <see cref="ModPermission.SpawnEntity"/> — see <see cref="IModEntitySpawn"/>
	/// for the full contract.
	/// </summary>
	IModEntitySpawn EntitySpawn { get; }

	/// <summary>
	/// The world item-spawn surface. Spawning requires
	/// <see cref="ModPermission.SpawnEntity"/> — see <see cref="IModItemSpawn"/>
	/// for the full contract.
	/// </summary>
	IModItemSpawn ItemSpawn { get; }

	/// <summary>
	/// The world tile/block placement surface. Placing requires
	/// <see cref="ModPermission.SpawnEntity"/> — see <see cref="IModTilePlacement"/>
	/// for the full contract.
	/// </summary>
	IModTilePlacement TilePlacement { get; }

	/// <summary>
	/// The multi-block structure placement surface. Placing requires
	/// <see cref="ModPermission.SpawnEntity"/> — see
	/// <see cref="IModStructurePlacement"/> for the full contract.
	/// </summary>
	IModStructurePlacement StructurePlacement { get; }

	/// <summary>
	/// The world liquid-tile placement/flood-fill surface. Placing requires
	/// <see cref="ModPermission.SpawnEntity"/> — see
	/// <see cref="IModLiquidPlacement"/> for the full contract.
	/// </summary>
	IModLiquidPlacement LiquidPlacement { get; }

	/// <summary>
	/// The permission-gated native/game-private operation registry. Invoking
	/// requires <see cref="ModPermission.AccessNativeApi"/> — see
	/// <see cref="IModNativeApi"/> for the full contract and value-safety policy.
	/// </summary>
	IModNativeApi NativeApi { get; }

	/// <summary>The first member handshake completed (host side: never — see the snapshot).</summary>
	event Action? SessionActivated;

	/// <summary>A member's handshake completed — each member exactly once, including yourself.</summary>
	event Action<ulong>? PlayerJoined;

	/// <summary>A member was removed (host side only; the host itself never leaves).</summary>
	event Action<ulong>? PlayerLeft;

	/// <summary>The session ended (host exit / EndSession).</summary>
	event Action? SessionEnded;
}
