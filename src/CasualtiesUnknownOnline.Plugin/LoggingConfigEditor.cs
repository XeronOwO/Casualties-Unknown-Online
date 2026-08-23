using BepInEx.Configuration;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Writes the CUO log-level config entry from the Online UI. The Runtime
/// already exposes the live <c>IOptionsMonitor&lt;LoggingOptions&gt;</c> through
/// BepInEx config; this plugin-side editor owns the concrete
/// <see cref="ConfigEntry{T}"/> so the Network page can change the level and
/// persist it immediately (the log providers re-read the monitor on every
/// write, so the change is live without a restart).
/// </summary>
internal sealed class LoggingConfigEditor
{
	private readonly ConfigFile _config;
	private readonly ConfigEntry<string> _minimumLevel;

	internal LoggingConfigEditor(ConfigFile config, ConfigEntry<string> minimumLevel)
	{
		_config = config;
		_minimumLevel = minimumLevel;
	}

	internal string Current => _minimumLevel.Value;

	internal void Set(string value)
	{
		_minimumLevel.Value = value;
		_config.Save();
	}
}
