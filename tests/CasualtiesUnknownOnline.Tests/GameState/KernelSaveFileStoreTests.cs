using System;
using System.IO;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class KernelSaveFileStoreTests
{
	private static readonly RunEpoch Epoch = new(7);
	private static readonly ActorId Host = new(1001);

	[Fact]
	public void SaveThenLoad_RoundTripsCheckpoint()
	{
		var path = Path.Combine(Path.GetTempPath(), $"cuo-kernel-save-{Guid.NewGuid():N}.bin");
		try
		{
			var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
			authority.ObserveSpawn(Host.Value, 42, "water", 1f, 2f);
			var checkpoint = authority.CreateCheckpoint();

			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance, gameBuild: "test", modBuild: "0.1.0");
			Assert.True(store.Save(checkpoint));
			Assert.True(store.TryLoad(out var loaded));
			Assert.Equal(checkpoint.RunEpoch.Value, loaded.RunEpoch.Value);
			Assert.Equal(checkpoint.GlobalRevision, loaded.GlobalRevision);
			var item = Assert.Single(loaded.Items);
			Assert.Equal(42ul, item.Identity.InstanceId);
			Assert.Equal(ItemLocationKind.World, item.Location.Kind);
		}
		finally
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
	}

	[Fact]
	public void SaveLoad_RoundTripsRandomStreams()
	{
		var path = Path.Combine(Path.GetTempPath(), $"cuo-kernel-save-random-{Guid.NewGuid():N}.bin");
		try
		{
			var checkpoint = new GameCheckpoint(
				Epoch,
				3,
				[],
				[new RandomStreamState("world-gen", "RLE", [11, 22, 33])]);
			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance);
			Assert.True(store.Save(checkpoint));
			Assert.True(store.TryLoad(out var loaded));
			var stream = Assert.Single(loaded.RandomStreams!);
			Assert.Equal("world-gen", stream.Name);
			Assert.Equal("RLE", stream.State);
			Assert.Equal([11ul, 22ul, 33ul], stream.DecidedValues);
		}
		finally
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
	}

	[Fact]
	public void MissingFile_FailsToLoad()
	{
		var store = new KernelSaveFileStore(
			Path.Combine(Path.GetTempPath(), $"cuo-kernel-missing-{Guid.NewGuid():N}.bin"),
			NullLogger<KernelSaveFileStore>.Instance);
		Assert.False(store.TryLoad(out _));
	}

	[Fact]
	public void DisabledStore_FailsToLoadAndSaveReturnsTrue()
	{
		var store = new KernelSaveFileStore(null, NullLogger<KernelSaveFileStore>.Instance);
		Assert.False(store.TryLoad(out _));
		Assert.True(store.Save(new GameCheckpoint(Epoch, 1, [])));
	}

	[Fact]
	public void CorruptFile_IsRejected()
	{
		var path = Path.Combine(Path.GetTempPath(), $"cuo-kernel-corrupt-{Guid.NewGuid():N}.bin");
		try
		{
			File.WriteAllText(path, "not a protobuf save");
			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance);
			Assert.False(store.TryLoad(out _));
		}
		finally
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
	}
}
