using System;

namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// The world archive's policy knobs (decision 25: the BepInEx <c>ConfigFile</c> is
/// bridged to <c>IOptionsMonitor&lt;T&gt;</c>, so a config edit hot-reloads without a
/// restart). The defaults are the ones frozen with the user on 2026-09-14 and already
/// written into the format doc §7: an interval autosave every 10 minutes, the newest
/// 10 archives kept.
///
/// The bounds are clamped HERE rather than trusted to the caller: a hand-edited config
/// file bypasses BepInEx's own range validation, and neither "autosave every 0 minutes"
/// nor "keep 0 archives" is a policy this system may execute — the first would cut every
/// frame, the second would delete the world's whole history.
/// </summary>
public sealed class SaveOptions
{
	/// <summary>The frozen default interval between interval autosaves (minutes).</summary>
	public const int DefaultAutosaveIntervalMinutes = 10;

	/// <summary>The frozen default number of backup archives one world keeps (§7).</summary>
	public const int DefaultBackupRetentionCount = 10;

	/// <summary>The shortest interval a config file may ask for (one minute).</summary>
	public const int MinAutosaveIntervalMinutes = 1;

	/// <summary>The longest interval a config file may ask for (24 hours — an autosave nobody would notice).</summary>
	public const int MaxAutosaveIntervalMinutes = 1440;

	/// <summary>The fewest archives a world may be reduced to: the newest one is never pruned, so 1 is a legal retention.</summary>
	public const int MinBackupRetentionCount = 1;

	/// <summary>An upper bound that keeps a typo from pinning a world's whole history on disk.</summary>
	public const int MaxBackupRetentionCount = 1000;

	/// <summary>
	/// Host: write an interval autosave while a world is being played. On by default
	/// (the format doc's "on by default, host-switchable"); turning it off leaves the
	/// player-initiated cuts, the layer-end cut and the menu-return cut in place —
	/// autosave is a safety net, not the only save.
	/// </summary>
	public bool AutosaveEnabled { get; set; } = true;

	/// <summary>Minutes between two interval autosaves, clamped to the legal range by <see cref="AutosaveInterval"/>.</summary>
	public int AutosaveIntervalMinutes { get; set; } = DefaultAutosaveIntervalMinutes;

	/// <summary>How many backup archives a world keeps, clamped by <see cref="Retention"/>.</summary>
	public int BackupRetentionCount { get; set; } = DefaultBackupRetentionCount;

	/// <summary>The configured interval as a duration, clamped to <see cref="MinAutosaveIntervalMinutes"/>..<see cref="MaxAutosaveIntervalMinutes"/>.</summary>
	public TimeSpan AutosaveInterval => TimeSpan.FromMinutes(Clamp(AutosaveIntervalMinutes, MinAutosaveIntervalMinutes, MaxAutosaveIntervalMinutes));

	/// <summary>The configured retention, clamped to <see cref="MinBackupRetentionCount"/>..<see cref="MaxBackupRetentionCount"/>.</summary>
	public int Retention => Clamp(BackupRetentionCount, MinBackupRetentionCount, MaxBackupRetentionCount);

	// net48 has no Math.Clamp; the two bounds are all this type needs.
	private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
}
