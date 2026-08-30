using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// Named full-configuration templates for BepInEx <see cref="ConfigFile"/>.
/// A profile captures every currently registered entry (language, log level,
/// IP-direct, host rules, state cadence, diagnostics, and any future preference
/// that is bound through the same file). Saving is a snapshot; applying writes
/// the captured values back through the same entries, so every
/// <c>IOptionsMonitor&lt;T&gt;</c> consumer sees the change through the normal
/// BepInEx SettingChanged path without a restart.
///
/// The store deliberately does not interpret individual settings: it is a
/// profile transport, not a schema. Unknown settings from an older/newer
/// profile are skipped when applying; missing settings are ignored. This keeps
/// templates usable across builds without pretending to migrate config schema.
/// </summary>
public sealed class ConfigurationProfileStore(
	ConfigFile config,
	string directory,
	ILogger<ConfigurationProfileStore> log)
{
	public const int CurrentSchemaVersion = 1;
	public const string ProfileFileExtension = ".profile";

	private readonly ConfigFile _config = config ?? throw new ArgumentNullException(nameof(config));
	private readonly string _directory = directory ?? throw new ArgumentNullException(nameof(directory));
	private readonly ILogger<ConfigurationProfileStore> _log = log ?? throw new ArgumentNullException(nameof(log));

	/// <summary>Captures the current full config and writes it as a named profile.</summary>
	public bool TrySaveCurrent(string name, out string error)
	{
		name = (name ?? "").Trim();
		if (!IsValidProfileName(name))
		{
			error = "Profile name must be 1-64 characters and may not contain path separators.";
			return false;
		}

		try
		{
			var document = new ConfigurationProfileDocument();
			foreach (var definition in _config.Keys)
			{
				var entry = _config[definition];
				if (entry is null)
				{
					continue;
				}

				document.Entries.Add(new ConfigurationProfileEntry
				{
					Section = definition.Section,
					Key = definition.Key,
					SerializedValue = entry.GetSerializedValue() ?? "",
				});
			}

			var path = GetPath(name);
			if (!WriteAtomically(document, path))
			{
				error = $"Could not write profile '{name}' to {path}.";
				return false;
			}

			_log.LogInformation("Saved configuration profile '{Name}' with {Count} entries to {Path}.", name, document.Entries.Count, path);
			error = "";
			return true;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Failed to save configuration profile '{Name}'.", name);
			error = ex.Message;
			return false;
		}
	}

	/// <summary>Applies a named profile to the live BepInEx config and persists it.</summary>
	public bool TryApply(string name, out string error)
	{
		name = (name ?? "").Trim();
		if (!IsValidProfileName(name))
		{
			error = "Profile name must be 1-64 characters and may not contain path separators.";
			return false;
		}

		var path = GetPath(name);
		if (!File.Exists(path))
		{
			error = $"No profile named '{name}'.";
			return false;
		}

		ConfigurationProfileDocument document;
		try
		{
			using var stream = File.OpenRead(path);
			document = Serializer.Deserialize<ConfigurationProfileDocument>(stream) ?? throw new InvalidDataException("Profile deserialized to null.");
			if (document.Version != CurrentSchemaVersion)
			{
				error = $"Profile '{name}' uses schema {document.Version}; this build reads {CurrentSchemaVersion}.";
				return false;
			}
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Failed to read configuration profile '{Name}' from {Path}.", name, path);
			error = $"Profile '{name}' could not be read: {ex.Message}";
			return false;
		}

		var applied = 0;
		var skipped = 0;
		var failed = 0;
		foreach (var entry in document.Entries)
		{
			if (entry is null || string.IsNullOrEmpty(entry.Section) || string.IsNullOrEmpty(entry.Key))
			{
				skipped++;
				continue;
			}

			var definition = new ConfigDefinition(entry.Section, entry.Key);
			if (!_config.ContainsKey(definition))
			{
				skipped++;
				continue;
			}

			try
			{
				_config[definition].SetSerializedValue(entry.SerializedValue);
				applied++;
			}
			catch (Exception ex)
			{
				failed++;
				_log.LogWarning(ex, "Failed to apply profile '{Name}' entry {Section}.{Key}.", name, entry.Section, entry.Key);
			}
		}

		try
		{
			_config.Save();
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Failed to save BepInEx config after applying profile '{Name}'.", name);
			error = ex.Message;
			return false;
		}

		_log.LogInformation("Applied configuration profile '{Name}': {Applied} applied, {Skipped} skipped, {Failed} failed.", name, applied, skipped, failed);
		if (failed > 0)
		{
			error = $"Profile '{name}' applied {applied} entries, skipped {skipped}, and failed on {failed}.";
			return false;
		}

		error = "";
		return true;
	}

	/// <summary>Deletes a named profile.</summary>
	public bool TryDelete(string name, out string error)
	{
		name = (name ?? "").Trim();
		if (!IsValidProfileName(name))
		{
			error = "Profile name must be 1-64 characters and may not contain path separators.";
			return false;
		}

		var path = GetPath(name);
		if (!File.Exists(path))
		{
			error = $"No profile named '{name}'.";
			return false;
		}

		try
		{
			File.Delete(path);
			_log.LogInformation("Deleted configuration profile '{Name}' ({Path}).", name, path);
			error = "";
			return true;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Failed to delete configuration profile '{Name}' from {Path}.", name, path);
			error = ex.Message;
			return false;
		}
	}

	/// <summary>Returns saved profile names sorted ordinally (case-insensitive by file name).</summary>
	public IReadOnlyList<string> ListProfiles()
	{
		if (!Directory.Exists(_directory))
		{
			return [];
		}

		return
		[
			.. Directory.GetFiles(_directory, "*" + ProfileFileExtension)
				.Select(Path.GetFileNameWithoutExtension)
				.Where(name => !string.IsNullOrEmpty(name))
				.Select(name => name!)
				.OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
		];
	}

	public bool Exists(string name) =>
		IsValidProfileName(name) && File.Exists(GetPath(name));

	private string GetPath(string name) => Path.Combine(_directory, name + ProfileFileExtension);

	private bool WriteAtomically(ConfigurationProfileDocument document, string path)
	{
		Directory.CreateDirectory(_directory);
		var tempPath = path + ".tmp";
		try
		{
			using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				Serializer.Serialize(stream, document);
				stream.Flush(true);
			}

			if (File.Exists(path))
			{
				File.Replace(tempPath, path, destinationBackupFileName: null);
			}
			else
			{
				File.Move(tempPath, path);
			}

			return true;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Failed to atomically write configuration profile to {Path}.", path);
			TryDeleteTemp(tempPath);
			return false;
		}
	}

	private static bool IsValidProfileName(string name) =>
		!string.IsNullOrWhiteSpace(name)
		&& name.Length <= 64
		&& name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
		&& name != "."
		&& name != "..";

	private void TryDeleteTemp(string tempPath)
	{
		try
		{
			if (File.Exists(tempPath))
			{
				File.Delete(tempPath);
			}
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Failed to remove the abandoned profile temp file {Path}.", tempPath);
		}
	}
}
