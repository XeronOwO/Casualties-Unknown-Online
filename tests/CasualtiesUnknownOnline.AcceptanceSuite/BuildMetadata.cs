using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CasualtiesUnknownOnline.AcceptanceSuite;

/// <summary>
/// The identities a replay compares: the game's Steam build id (the value that changes on a game
/// patch), the deployed plug-in's file version and the artifact string the deploy script prints.
/// A missing file is "unknown", never an error — a report from a machine that has not deployed yet
/// still names what it saw.
/// </summary>
internal sealed record BuildMetadata(string? GameBuild, string? ModBuild, string? Artifact)
{
	public static BuildMetadata Read(string? gameDir)
	{
		if (string.IsNullOrWhiteSpace(gameDir))
		{
			return new BuildMetadata(null, null, null);
		}

		var plugin = Path.Combine(gameDir, "BepInEx", "plugins", "CasualtiesUnknownOnline", "CasualtiesUnknownOnline.dll");
		return new BuildMetadata(GameBuildOf(gameDir), VersionOf(plugin, product: false), VersionOf(plugin, product: true));
	}

	/// <summary>
	/// The Steam library's app manifest carries the installed build id and the install folder; the
	/// folder match keeps this independent of a hard-coded app id. The exe's ProductVersion is the
	/// Unity player's version, not the game's patch, so it is never reported as the game build.
	/// </summary>
	private static string? GameBuildOf(string gameDir)
	{
		var full = Path.GetFullPath(gameDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		var folder = Path.GetFileName(full);
		var steamApps = Path.GetFullPath(Path.Combine(full, "..", ".."));
		try
		{
			foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
			{
				var text = File.ReadAllText(manifest);
				if (!Regex.IsMatch(text, "\"installdir\"\\s+\"" + Regex.Escape(folder) + "\"", RegexOptions.IgnoreCase))
				{
					continue;
				}

				var match = Regex.Match(text, "\"buildid\"\\s+\"(\\d+)\"");
				if (match.Success)
				{
					return match.Groups[1].Value;
				}
			}
		}
		catch (Exception)
		{
		}

		return null;
	}

	private static string? VersionOf(string path, bool product)
	{
		try
		{
			if (!File.Exists(path))
			{
				return null;
			}

			var info = FileVersionInfo.GetVersionInfo(path);
			var value = product ? info.ProductVersion : info.FileVersion;
			return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}
		catch (Exception)
		{
			return null;
		}
	}
}
