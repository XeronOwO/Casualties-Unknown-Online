using CasualtiesUnknownOnline.Runtime.Configuration;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The host-rules surface in force for THIS process. It composes the two config
/// sections the rules live in (<see cref="HostRulesOptions"/> and
/// <see cref="RespawnOptions"/>) into one read-only set, and resolves which process's
/// values those are: the host's own configuration on a host, and on a MEMBER the set
/// the host last announced, because the rules it reads govern what its screen offers,
/// what its console lets it do and what its own carried body weighs.
///
/// <para>
/// The fallback is deliberate and is not "the host's values are unknown, so guess":
/// a member that has received no announcement reads its own configuration, which is
/// exactly the behaviour that existed before the rules were replicated, and its local
/// checks stay skipped (<see cref="AnnouncedHostRules.Forbids"/> answers false) so the
/// host's own verdict is the only one. The host never receives an announcement (the
/// wire direction refuses one), so the host's enforcement paths
/// (<c>HandshakeHandler</c>, <c>PlayerInventoryTakeService</c>,
/// <c>SessionMemberCommandRules</c>) keep reading the host's own config unchanged.
/// </para>
///
/// <para>
/// The service stays stateless over the monitors: both are read at property access
/// time, so a config edit hot-reloads immediately, and <see cref="Current"/> re-reads
/// them on every access rather than caching a snapshot the monitors would invalidate.
/// </para>
/// </summary>
public sealed class HostRulesService(
	IOptionsMonitor<HostRulesOptions> hostRules,
	IOptionsMonitor<RespawnOptions> respawn,
	AnnouncedHostRules announced) : IHostRules
{
	private readonly IOptionsMonitor<HostRulesOptions> _hostRules = hostRules;
	private readonly IOptionsMonitor<RespawnOptions> _respawn = respawn;
	private readonly AnnouncedHostRules _announced = announced;

	/// <summary>
	/// The rule set in force: the announced set when this client has received one,
	/// otherwise this process's own configuration (the host's, or a member's own before
	/// anything was announced).
	/// </summary>
	public HostRuleSet Current => _announced.Current
		?? HostRuleSet.FromOptions(_hostRules.CurrentValue, _respawn.CurrentValue);

	public bool PvpEnabled => Current.PvpEnabled;

	public bool AutoContinue => Current.AutoContinue;

	public bool AllowLateJoin => Current.AllowLateJoin;

	public RemoteBackpackAccess RemoteBackpackAccess => Current.RemoteBackpackAccess;

	public bool AllowMemberCheatVerbs => Current.AllowMemberCheatVerbs;

	public bool WidenRunSettings => Current.WidenRunSettings;

	public float PiggybackWeightMultiplier => Current.PiggybackWeightMultiplier;

	public NativeBindingParity NativeBindingParity => Current.NativeBindingParity;

	public bool SaveInventory => Current.SaveInventory;

	public bool ReviveFromTrader => Current.ReviveFromTrader;

	public bool ReviveOnNextLevel => Current.ReviveOnNextLevel;

	public bool Permadeath => Current.Permadeath;

	/// <inheritdoc />
	public bool RadiationLineStopsAboveTheFloor => Current.RadiationLineStopsAboveTheFloor;
}
