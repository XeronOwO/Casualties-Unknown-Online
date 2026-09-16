using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The on-disk name and layout constants of the world archive
/// (docs/architecture/save-archive-format.md §2–§3): the world-folder layout,
/// the immutable <c>worldId</c> / backup-file grammar, and the build formats
/// used for timestamps and archive stems. Every other persistence type reads
/// its names from here — a literal <c>"manifest.json"</c> outside this file is
/// a bug.
/// </summary>
public static class SaveArchiveFormat
{
	/// <summary>The repository root below the CUO data root (§1).</summary>
	public const string SavesFolderName = "saves";

	/// <summary>Per-world file names.</summary>
	public const string MetadataFileName = "world.json";

	public const string LiveFolderName = "live";
	public const string ManifestFileName = "manifest.json";
	public const string BackupsFolderName = "backups";
	public const string StagingFolderName = ".staging";
	public const string PreviousFolderName = ".previous";
	public const string IndexFileName = "index.json";

	/// <summary>The writer lease a world folder carries while a process is writing it (§5 / <see cref="WorldLease"/>).</summary>
	public const string LeaseFileName = "world.lease";

	/// <summary>
	/// Prefix of the folder a REFUSED live snapshot is preserved under when a restore
	/// promotes a backup over it (§6 / <see cref="WorldBackupPromotion"/>). It stays inside
	/// the world folder, and nothing else in the layout ever writes or deletes it: the
	/// evidence of a snapshot CUO could not open must not be destroyed by the next cut.
	/// </summary>
	public const string DamagedFolderPrefix = "damaged-";

	/// <summary>Backup archive extension: ZIP with directory entries preserved (§2).</summary>
	public const string BackupExtension = ".cuoz";

	/// <summary>Path of <c>characters/&lt;playerKey&gt;.json</c> inside a snapshot (§3.4).</summary>
	public const string CharactersFolderName = "characters";

	/// <summary>Character files are JSON like every other payload file.</summary>
	public const string CharacterFileExtension = ".json";

	/// <summary>Snapshot-relative path of one player's character file (§3.4). The key is transport-scoped (decision 162).</summary>
	public static string CharacterFilePath(string playerKey) =>
		CharactersFolderName + "/" + playerKey + CharacterFileExtension;

	/// <summary>True = the snapshot-relative path is a <c>characters/&lt;playerKey&gt;.json</c> file.</summary>
	public static bool IsCharacterPath(string? path) =>
		!string.IsNullOrEmpty(path)
		&& path!.StartsWith(CharactersFolderName + "/", StringComparison.Ordinal)
		&& path.EndsWith(CharacterFileExtension, StringComparison.OrdinalIgnoreCase);

	/// <summary>The player key a character file's path carries, or "" when the path is not one.</summary>
	public static string PlayerKeyOfCharacterPath(string path)
	{
		if (!IsCharacterPath(path))
		{
			return string.Empty;
		}

		var name = path.Substring(CharactersFolderName.Length + 1);
		return name.Substring(0, name.Length - CharacterFileExtension.Length);
	}

	// ---- Domain files (§3.4): one file per domain table, every file an entry array ----

	public const string RunFileName = "run.json";
	public const string PlayersFileName = "players.json";
	public const string ItemsFileName = "items.json";
	public const string WorldEntitiesFileName = "world-entities.json";
	public const string EnemiesFileName = "enemies.json";
	public const string FluidsFileName = "fluids.json";

	/// <summary>S3's in-layer block diff; S2 writes the empty form.</summary>
	public const string WorldBlocksFileName = "world-blocks.json";

	/// <summary>S3's transient world facts; S2 writes the empty form.</summary>
	public const string WorldTransientsFileName = "world-transients.json";

	/// <summary>Reserved empty directory for a later stage (§3.4).</summary>
	public const string ModStateFolderName = "mod-state";

	/// <summary>Backup stem format: <c>&lt;kind&gt;-&lt;stamp&gt;</c>, e.g. <c>layer-end-20260910-120000</c>.</summary>
	public const string BackupStampFormat = "yyyyMMdd-HHmmss";

	/// <summary>UTC format of the <c>*Utc</c> DTO fields — sortable and timezone-free.</summary>
	public const string UtcTimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";

	internal const string WorldIdFormat = @"w-yyyyMMdd-<4 hex>";

	// The stamp is anchored explicitly: kind and stamp are both 'word-word' shapes,
	// so an unanchored pattern splits "layer-end-20260910-120000" as
	// kind="layer-end-20260910-1200", stamp="00-120000".
	private static readonly Regex BackupNameRegex = new(
		@"^(?<kind>[a-z]+(?:-[a-z]+)*)-(?<stamp>\d{8}-\d{6})(?:-(?<suffix>\d+?))?$",
		RegexOptions.CultureInvariant);

	private static readonly Regex WorldIdRegex = new(
		@"^w-\d{8}-[0-9a-f]{4}$",
		RegexOptions.CultureInvariant);

	/// <summary>Formats a timestamp the way every archive field does.</summary>
	public static string FormatUtc(DateTime utc) =>
		utc.ToUniversalTime().ToString(UtcTimestampFormat, CultureInfo.InvariantCulture);

	/// <summary>Parses a timestamp written by <see cref="FormatUtc"/>. False = not this format (never guessed).</summary>
	public static bool TryParseUtc(string? text, out DateTime utc)
	{
		utc = default;
		return !string.IsNullOrEmpty(text)
			&& DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
	}

	/// <summary>True = the folder name is a generated, immutable world id.</summary>
	public static bool IsWorldId(string? folderName) =>
		!string.IsNullOrEmpty(folderName) && WorldIdRegex.IsMatch(folderName!);

	/// <summary>The <c>yyyyMMdd-HHmmss</c> stamp a backup name (and a preserved snapshot's folder) carries (§2).</summary>
	public static string StampOf(DateTime utc) =>
		utc.ToUniversalTime().ToString(BackupStampFormat, CultureInfo.InvariantCulture);

	/// <summary>Builds the backup stem for a cut kind and stamp (§2, §7).</summary>
	public static string BuildBackupStem(WorldCutKind kind, DateTime utc) =>
		$"{CutKindName(kind)}-{StampOf(utc)}";

	/// <summary>Parses a backup file name (<c>&lt;kind&gt;-&lt;stamp&gt;[-n].cuoz</c>). False = not a backup of this format.</summary>
	public static bool TryParseBackupFileName(string? fileName, out WorldCutKind kind, out string stem)
	{
		kind = default;
		stem = string.Empty;
		if (string.IsNullOrEmpty(fileName) || !fileName!.EndsWith(BackupExtension, StringComparison.Ordinal))
		{
			return false;
		}

		stem = fileName.Substring(0, fileName.Length - BackupExtension.Length);
		var match = BackupNameRegex.Match(stem);
		return match.Success && TryParseCutKind(match.Groups["kind"].Value, out kind);
	}

	/// <summary>The stamp part of a backup stem (<c>yyyyMMdd-HHmmss</c>), or "" when the stem is not this format.</summary>
	internal static string StampOfStem(string? stem)
	{
		var match = stem is null ? Match.Empty : BackupNameRegex.Match(stem);
		return match.Success ? match.Groups["stamp"].Value : string.Empty;
	}

	/// <summary>The disambiguating suffix of a backup stem (0 when it has none).</summary>
	internal static int SuffixOfStem(string? stem)
	{
		var match = stem is null ? Match.Empty : BackupNameRegex.Match(stem);
		return match.Success && match.Groups["suffix"].Success && int.TryParse(match.Groups["suffix"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var suffix)
			? suffix
			: 0;
	}

	/// <summary>The stem a backup of <paramref name="fileName"/> would have, or "" when the name is not this format.</summary>
	internal static string StemOf(string? fileName) =>
		!string.IsNullOrEmpty(fileName) && fileName!.EndsWith(BackupExtension, StringComparison.Ordinal)
			? fileName.Substring(0, fileName.Length - BackupExtension.Length)
			: string.Empty;

	/// <summary>True = the text is one of the manifest's cut kinds.</summary>
	public static bool TryParseCutKind(string? text, out WorldCutKind kind)
	{
		kind = default;

		// The format's spelling is kebab-case ("layer-end"); the enum member is
		// "LayerEnd", so the separator is removed rather than mapped to '_'.
		return !string.IsNullOrEmpty(text)
			&& Enum.TryParse(text!.Replace("-", string.Empty), ignoreCase: true, out kind);
	}

	/// <summary>The manifest/JSON spelling of a cut kind (<c>layer-end</c>, <c>mid-run</c>, <c>auto</c>).</summary>
	public static string CutKindName(WorldCutKind kind) => kind switch
	{
		WorldCutKind.LayerEnd => "layer-end",
		WorldCutKind.MidRun => "mid-run",
		WorldCutKind.Auto => "auto",
		_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cut kind; the JSON spelling is part of the format."),
	};

	/// <summary>The manifest's <c>saveReason</c> spelling (§3.2) — the trigger a cut records as its provenance.</summary>
	public static string CutReasonName(WorldCutReason reason) => reason switch
	{
		WorldCutReason.LayerAdvance => "layer-advance",
		WorldCutReason.MenuReturn => "menu-return",
		WorldCutReason.Command => "command",
		WorldCutReason.AutoInterval => "auto-interval",
		WorldCutReason.PreRestoreBackup => "pre-restore-backup",
		_ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown cut reason; the JSON spelling is part of the format."),
	};
}
