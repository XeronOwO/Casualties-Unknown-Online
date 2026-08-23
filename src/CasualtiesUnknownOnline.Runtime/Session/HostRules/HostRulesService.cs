using CasualtiesUnknownOnline.Runtime.Configuration;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The minimal host-rules service. It composes the host-only
/// <see cref="HostRulesOptions"/> and the already-landed
/// <see cref="RespawnOptions"/> into one read-only surface, so future host-rule
/// consumers (PVP, auto-continue, late-join gates) ask one small service instead
/// of reaching into multiple config sections. The service is stateless and
/// reads both option monitors at property access time, so config hot-reloads
/// are immediately visible.
/// </summary>
public sealed class HostRulesService(
	IOptionsMonitor<HostRulesOptions> hostRules,
	IOptionsMonitor<RespawnOptions> respawn) : IHostRules
{
	private readonly IOptionsMonitor<HostRulesOptions> _hostRules = hostRules;
	private readonly IOptionsMonitor<RespawnOptions> _respawn = respawn;

	public bool PvpEnabled => _hostRules.CurrentValue.PvpEnabled;

	public bool AutoContinue => _hostRules.CurrentValue.AutoContinue;

	public bool AllowLateJoin => _hostRules.CurrentValue.AllowLateJoin;

	public bool SaveInventory => _respawn.CurrentValue.RespawnKeepInventory;

	public bool ReviveFromTrader => _respawn.CurrentValue.ReviveFromTrader;

	public bool ReviveOnNextLevel => _respawn.CurrentValue.ReviveOnNextLevel;

	public bool Permadeath => _respawn.CurrentValue.Permadeath;
}
