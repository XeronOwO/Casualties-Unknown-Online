namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// The host-rule flags that are not already owned by <see cref="RespawnOptions"/>.
/// The host rules service composes this with <see cref="RespawnOptions"/> into
/// one read-only host-rules surface. Backed by <c>IOptionsMonitor&lt;T&gt;</c>
/// so a BepInEx config edit hot-reloads without a restart.
/// </summary>
public sealed class HostRulesOptions
{
	/// <summary>
	/// Player-vs-player damage is enabled. The PVP damage domain is intentionally
	/// not built yet (backlog §2.6); this flag is the host-rule surface reserved
	/// for that future domain and currently has no gameplay effect.
	/// </summary>
	public bool PvpEnabled { get; set; }

	/// <summary>
	/// Automatically continue to the next world layer after generation finishes
	/// instead of requiring the host to click again. Reserved host-rule surface;
	/// the auto-continue flow is not wired yet.
	/// </summary>
	public bool AutoContinue { get; set; }

	/// <summary>
	/// A brand-new player may handshake and enter the host's already-running
	/// world. Disabling it rejects new members while the host is in-world;
	/// reconnects of already-known members are always allowed.
	/// </summary>
	public bool AllowLateJoin { get; set; } = true;

	/// <summary>
	/// Host-only: whether other players may take carried items from a remote
	/// player's inventory at all. Keeping the default true preserves the
	/// cooperative rule (an unconscious/dead body may be looted); turning it
	/// off disables the cross-player take buttons and the host-side authority
	/// path completely.
	/// </summary>
	public bool AllowRemoteInventoryTake { get; set; } = true;

	/// <summary>
	/// Host-only: widen the native custom run-settings sliders in co-op so the
	/// run can be tuned for the actual lobby size. The slider limits are UI-only;
	/// selected values still ride the existing world-start params unchanged.
	/// </summary>
	public bool WidenRunSettings { get; set; } = true;

	/// <summary>
	/// Host-only: the fraction of a carried/rider player's full encumbrance that
	/// is added to the carrier's own encumbrance while a carry/piggyback relation
	/// is active. Matches KrokMP's <c>PiggybackWeightMultiplier</c> rule spirit:
	/// 0 disables the movement penalty, 0.8 is the KrokMP default.
	/// </summary>
	public float PiggybackWeightMultiplier { get; set; } = 0.8f;

	/// <summary>
	/// Host-only: the CHEAT SWITCH of the family — whether members may run the cheat
	/// verbs at all (<c>/data</c>'s write verbs, <c>/give</c> and <c>/summon</c>). OFF
	/// by default, because the family is deliberately "not a cheating surface by
	/// default" (`done/item-and-entity-data-commands.md`).
	///
	/// <para>
	/// It is one of TWO host rulings a member's cheat verb needs, and it is the broader
	/// one: this switch says the family is open, and the cheat roster
	/// (<c>CheatPermissions</c>, edited by <c>/op</c> and <c>/deop</c>) says WHO may
	/// ask. A granted member is refused while the switch is off — the user's
	/// 2026-10-18 ruling — and the switch alone admits nobody; the host's own verbs
	/// bypass both, because its standing is the online switch.
	/// </para>
	///
	/// <para>
	/// The switch follows what a member may REPORT on at all rather than what it
	/// happens to hold: its own carried, installed or contained item, or world
	/// content — the same item set a native use may report, because a console write is
	/// that same kind of fact (see <c>KernelCommandGateway.MayReportOn</c>). It is a
	/// rule over the VERB, never a check on the value: the pre-release model still
	/// trusts a member's report of its own item's state, and questioning the state
	/// itself is S3's stage (<c>todo/validation-and-source-checks.md</c>).
	/// </para>
	///
	/// <para>
	/// It is enforced on BOTH sides: the member's own console refuses a cheat verb
	/// while the announced switch is off (no round trip, the reason where the operator
	/// typed), and the HOST's admission seam judges it again — the member's console can
	/// never be the gate. A refused request is answered by name, and the member's client
	/// puts its already-changed local value back — with this off, a member's
	/// <c>/data set</c> therefore reads the value it wrote, then reads the refusal and
	/// the restored value in the same console.
	/// </para>
	/// </summary>
	public bool AllowMemberCheatVerbs { get; set; }

	/// <summary>
	/// Host-only: how a member's declared native binding is judged when it differs
	/// from the host's own for a mod both sides list (allow / warn / require).
	/// Warn is the default on purpose: the declaration is new, so a host that
	/// refused by default would lock out every session whose host updated first.
	/// </summary>
	public NativeBindingParity NativeBindingParity { get; set; } = NativeBindingParity.Warn;

	/// <summary>
	/// Host-only: the radiation line stops a margin above the layer floor instead of
	/// descending past it, so the bottom of a layer stays livable and a group can share
	/// supplies and rest there. ON by default, which is the behaviour the user asked to
	/// align with; turning it off restores the game's own uncapped descent.
	/// </summary>
	public bool RadiationLineStopsAboveTheFloor { get; set; } = true;

	/// <summary>Host-only: a spectating player may free-fly the camera instead of only following a teammate. ON by default, aligned with the reference mod: a free camera can scout the layer ahead, so it is a group decision rather than one client's rendering choice.</summary>
	public bool SpectatorFreeFly { get; set; } = true;

	/// <summary>Host-only: an UNCONSCIOUS player may spectate. OFF by default: in this project's model an unconscious body is still its owner's to act on, so this changes a live player's client rather than a dead one's.</summary>
	public bool SpectateWhileUnconscious { get; set; } = false;

	/// <summary>Host-only: a client that has not entered the world yet may spectate. OFF by default: that is the late-join case, and it is a separate promise about what a not-yet-in-world client may see.</summary>
	public bool SpectateWhileWaitingToJoin { get; set; } = false;
}
