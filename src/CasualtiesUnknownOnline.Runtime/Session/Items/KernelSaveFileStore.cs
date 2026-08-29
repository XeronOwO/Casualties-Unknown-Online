using System;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Protocol.Wire;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Phase C checkpoint disk store. The save format carries a
/// <see cref="SaveHeader"/> plus the authoritative kernel checkpoint; old
/// formats are rejected (no silent migration) because the project has no
/// compatibility burden and old DTOs must not pollute the new model.
/// </summary>
public sealed class KernelSaveFileStore(string? filePath, ILogger<KernelSaveFileStore> log, string? gameBuild = null, string? modBuild = null)
{
	public const int CurrentSchemaVersion = 1;

	private readonly string? _filePath = filePath;
	private readonly ILogger<KernelSaveFileStore> _log = log;
	private readonly string _gameBuild = string.IsNullOrEmpty(gameBuild) ? "Unknown" : gameBuild!;
	private readonly string _modBuild = string.IsNullOrEmpty(modBuild) ? "Unknown" : modBuild!;

	public bool IsEnabled => !string.IsNullOrEmpty(_filePath);

	public bool Save(GameCheckpoint checkpoint)
	{
		if (!IsEnabled)
		{
			return true;
		}

		var file = new KernelSaveFile
		{
			Header = new SaveHeader
			{
				SchemaVersion = CurrentSchemaVersion,
				GameBuild = _gameBuild,
				ModBuild = _modBuild,
				RunEpoch = checkpoint.RunEpoch.Value,
				GlobalRevision = checkpoint.GlobalRevision,
				CreatedAtTicks = DateTime.UtcNow.Ticks,
			},
			Items = [.. checkpoint.Items.Select(KernelWireMapper.ToWireItem)],
			RandomStreams = [.. checkpoint.RandomStreams?.Select(ToWireRandomStream) ?? []],
			Run = checkpoint.Run is null ? null : KernelWireMapper.ToWireRun(checkpoint.Run),
			WorldEntities = checkpoint.WorldEntities is null ? null : KernelWireMapper.ToWireWorldEntityState(checkpoint.WorldEntities),
		};

		return WriteAtomically(file);
	}

	public bool TryLoad(out GameCheckpoint checkpoint)
	{
		checkpoint = new GameCheckpoint(new RunEpoch(0), 0, []);
		if (!IsEnabled || !File.Exists(_filePath!))
		{
			return false;
		}

		try
		{
			using var stream = File.OpenRead(_filePath!);
			var file = Serializer.Deserialize<KernelSaveFile>(stream);
			if (file is null)
			{
				_log.LogWarning("Kernel save file {Path} deserialized to null — rejected.", _filePath);
				return false;
			}

			if (file.Header.SchemaVersion != CurrentSchemaVersion)
			{
				_log.LogWarning("Kernel save file {Path} has schema {Schema}; this build reads {Current} — rejected.",
					_filePath, file.Header.SchemaVersion, CurrentSchemaVersion);
				return false;
			}

			if (file.Header.RunEpoch == 0)
			{
				_log.LogWarning("Kernel save file {Path} has an invalid run epoch 0 — rejected.", _filePath);
				return false;
			}

			checkpoint = new GameCheckpoint(
				new RunEpoch(file.Header.RunEpoch),
				file.Header.GlobalRevision,
				[.. file.Items.Select(KernelWireMapper.FromWireItem)],
				[.. file.RandomStreams.Select(FromWireRandomStream)],
				file.Run is null ? null : KernelWireMapper.FromWireRun(file.Run),
				file.WorldEntities is null ? null : KernelWireMapper.FromWireWorldEntityState(file.WorldEntities));
			_log.LogInformation("Loaded kernel checkpoint from {Path}: epoch {Epoch}, revision {Revision}, items {Items}.",
				_filePath, checkpoint.RunEpoch.Value, checkpoint.GlobalRevision, checkpoint.Items.Count);
			return true;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Kernel save file {Path} is corrupt or unreadable — rejected.", _filePath);
			return false;
		}
	}

	private static WireRandomStream ToWireRandomStream(RandomStreamState state) =>
		new()
		{
			Name = state.Name,
			State = state.State,
			DecidedValues = [.. state.DecidedValues],
		};

	private static RandomStreamState FromWireRandomStream(WireRandomStream stream) =>
		new(stream.Name, stream.State, [.. stream.DecidedValues]);

	private bool WriteAtomically(KernelSaveFile file)
	{
		var path = _filePath!;
		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		var tempPath = path + ".tmp";
		try
		{
			using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				Serializer.Serialize(stream, file);
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
			_log.LogWarning(ex, "Failed to write kernel save file {Path} — the in-memory checkpoint remains authoritative.", path);
			TryDeleteTemp(tempPath);
			return false;
		}
	}

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
			_log.LogWarning(ex, "Failed to remove the abandoned kernel-save temp file {Path}.", tempPath);
		}
	}
}
