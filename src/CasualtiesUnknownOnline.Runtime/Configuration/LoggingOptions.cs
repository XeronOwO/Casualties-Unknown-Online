using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// Runtime logging configuration. The minimum level is enforced by the log
/// PROVIDERS (BepInEx + rolling file), not by the logging builder, so a
/// config-backed <c>IOptionsMonitor</c> can change it live without rebuilding
/// the container. Default: Information — normal play stays quiet (the old
/// providers forwarded Trace/Debug unconditionally), a local dev raises Debug
/// or Trace in <c>BepInEx/config/CasualtiesUnknownOnline.cfg</c>.
/// </summary>
public sealed class LoggingOptions
{
	/// <summary>The lowest level the CUO log providers write.</summary>
	public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
}
