using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The character-data disk file's pure store contract: full-field round-trip,
/// missing-file and schema degradation, delete, and the atomic-write temp
/// contract (no .tmp residue after a successful write).
/// </summary>
public class CharacterDataFileStoreTests
{
	private static string NewPath() =>
		Path.Combine(Path.GetTempPath(), "cuo-tests", "character-data", Guid.NewGuid().ToString("N"), "characters.bin");

	private static Dictionary<ulong, CharacterDataMsg> Table(ulong steamId, CharacterDataMsg data) => new()
	{
		[steamId] = data,
	};

	private static CharacterDataMsg FullSnapshot(ulong owner) => new()
	{
		OwnerSteamId = owner,
		Skills = new CharacterSkillsMsg
		{
			Strength = 7,
			Resistance = 3,
			Intelligence = 5,
			ExpStrength = 12.5f,
			ExpResistance = 4.25f,
			ExpIntelligence = 9.75f,
		},
		Health = new CharacterHealthMsg
		{
			BloodVolume = 5.1f,
			Hunger = 61.5f,
			Shock = 20f,
			SepticShock = 3f,
			EyePanicTime = 0.5f,
			HorrifiedLevel = 2f,
			Alive = true,
			Conscious = true,
			Disfigured = true,
			EyeGone = true,
			BothEyesGone = true,
			DisfiguredIndex = 2,
			DisfiguredTimeFullSkin = 123.5f,
			EyeTimeHealed = 456.25f,
		},
		Limbs =
		[
			new CharacterLimbMsg
			{
				Index = 0,
				SkinHealth = 80f,
				MuscleHealth = 70f,
				Broken = true,
				Infected = true,
				BleedAmount = 1.5f,
				Pain = 12f,
				Shrapnel = 2,
				Components =
				[
					new ComponentStateMsg
					{
						TypeName = "SplintLimb",
						Fields = [new ComponentFieldMsg { Name = "condition", Kind = 1, FloatValue = 0.5f }],
					},
				],
			},
		],
		Items =
		[
			new CharacterItemMsg
			{
				InstanceId = 4242,
				ItemId = "flashlight",
				Condition = 0.75f,
				SlotIndex = 3,
				Favourited = true,
				Components = [new ComponentStateMsg { TypeName = "TestComponent", Fields = [new ComponentFieldMsg { Name = "charges", Kind = 2, IntValue = 3 }] }],
				Liquids = [new LiquidStackMsg { LiquidId = "water", Amount = 0.5f }],
				Contents =
				[
					new CharacterItemMsg
					{
						InstanceId = 4243,
						ItemId = "battery",
						Condition = 0.9f,
						SlotIndex = 0,
					},
				],
			},
		],
		HandSlot = 3,
		Position = new NetVector2Msg { X = 12.5f, Y = 34.75f },
	};

	[Fact]
	public void Save_Load_RoundTripsEveryFieldFamily()
	{
		var path = NewPath();
		try
		{
			var store = new CharacterDataFileStore(path, NullLogger<CharacterDataFileStore>.Instance);
			var data = FullSnapshot(42);

			Assert.True(store.Save(Table(42, data)), "save must succeed");
			Assert.False(File.Exists(path + ".tmp"), "a successful atomic write must not leave its temp file");

			Assert.True(store.TryLoad(out var loaded), "load must succeed");
			Assert.True(loaded.Count == 1, $"expected one entry, got {loaded.Count}");
			var restored = loaded[42];
			Assert.Equal(42UL, restored.OwnerSteamId);
			Assert.Equal(7, restored.Skills!.Strength);
			Assert.Equal(61.5f, restored.Health!.Hunger);
			Assert.True(restored.Health!.Disfigured, "the disfigured latch must survive the round-trip");
			Assert.True(restored.Health!.EyeGone, "the eyeGone latch must survive the round-trip");
			Assert.True(restored.Health!.BothEyesGone, "the bothEyesGone latch must survive the round-trip");
			Assert.Equal(2, restored.Health!.DisfiguredIndex);
			Assert.Equal(123.5f, restored.Health!.DisfiguredTimeFullSkin);
			Assert.Equal(456.25f, restored.Health!.EyeTimeHealed);
			Assert.True(restored.Limbs.Count == 1, "the limb must survive the round-trip");
			Assert.True(restored.Limbs[0].Broken, "the limb bool must survive the round-trip");
			Assert.Equal(12f, restored.Limbs[0].Pain);
			var limbComponent = Assert.Single(restored.Limbs[0].Components);
			Assert.Equal("SplintLimb", limbComponent.TypeName);
			Assert.Equal(0.5f, Assert.Single(limbComponent.Fields).FloatValue);
			Assert.Equal(4242UL, restored.Items[0].InstanceId);
			Assert.True(restored.Items[0].Contents.Count == 1, "nested container contents must survive the round-trip");
			Assert.Equal("water", restored.Items[0].Liquids[0].LiquidId);
			Assert.Equal(3, restored.HandSlot);
			Assert.Equal(12.5f, restored.Position!.X);
			Assert.Equal(34.75f, restored.Position.Y);
		}
		finally
		{
			DeletePath(path);
		}
	}

	[Fact]
	public void Load_MissingFile_IsAnEmptyTable()
	{
		var path = NewPath();
		try
		{
			var store = new CharacterDataFileStore(path, NullLogger<CharacterDataFileStore>.Instance);

			Assert.True(store.TryLoad(out var loaded), "a missing file is a settled empty load");
			Assert.True(loaded.Count == 0, "a missing file must read as empty");
		}
		finally
		{
			DeletePath(path);
		}
	}

	[Fact]
	public void Load_CorruptFile_DegradesToEmpty()
	{
		var path = NewPath();
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllBytes(path, [0x00, 0x01, 0x02, 0xFF]);
			var store = new CharacterDataFileStore(path, NullLogger<CharacterDataFileStore>.Instance);

			Assert.False(store.TryLoad(out var loaded), "a corrupt file must report the failed load");
			Assert.True(loaded.Count == 0, "a corrupt file must read as empty");
			Assert.True(File.Exists(path), "a corrupt file must NOT be deleted by the failed load");
		}
		finally
		{
			DeletePath(path);
		}
	}

	[Fact]
	public void Load_UnknownVersion_DegradesToEmpty()
	{
		var path = NewPath();
		try
		{
			var store = new CharacterDataFileStore(path, NullLogger<CharacterDataFileStore>.Instance);
			Assert.True(store.Save(Table(42, FullSnapshot(42))), "seed save must succeed");

			var incompatible = new CharacterDataFile
			{
				Version = CharacterDataFile.CurrentVersion + 1,
				Characters = [new CharacterDataFile.Entry { SteamId = 42, Data = FullSnapshot(42) }],
			};
			using (var stream = File.Create(path))
			{
				ProtoBuf.Serializer.Serialize(stream, incompatible);
			}

			Assert.False(store.TryLoad(out var loaded), "an unknown version must be refused");
			Assert.True(loaded.Count == 0, "an unknown version must read as empty");
		}
		finally
		{
			DeletePath(path);
		}
	}

	[Fact]
	public void Save_EmptyTable_RoundTripsAsTheNewRunTombstone()
	{
		var path = NewPath();
		try
		{
			var store = new CharacterDataFileStore(path, NullLogger<CharacterDataFileStore>.Instance);

			Assert.True(store.Save([]), "the empty-table tombstone save must succeed");
			Assert.True(store.TryLoad(out var loaded), "the tombstone must load");
			Assert.True(loaded.Count == 0, "the tombstone must read as an empty new run");
		}
		finally
		{
			DeletePath(path);
		}
	}


	[Fact]
	public void Delete_RemovesTheFile()
	{
		var path = NewPath();
		try
		{
			var store = new CharacterDataFileStore(path, NullLogger<CharacterDataFileStore>.Instance);
			Assert.True(store.Save(Table(42, FullSnapshot(42))), "seed save must succeed");
			Assert.True(File.Exists(path), "the seed file must exist");

			Assert.True(store.Delete(), "delete must succeed");
			Assert.False(File.Exists(path), "the file must be gone after delete");
		}
		finally
		{
			DeletePath(path);
		}
	}

	private static void DeletePath(string path)
	{
		DeleteFileIfExists(path);
		DeleteFileIfExists(path + ".tmp");
		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
		{
			Directory.Delete(directory); // only after the two known files are gone — never recursive
		}
	}

	private static void DeleteFileIfExists(string path)
	{
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}
}
