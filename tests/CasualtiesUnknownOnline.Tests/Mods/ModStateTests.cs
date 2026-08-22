using System;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The phase-4 mod-state surface: a host-only, per-mod opaque key/value store
/// persisted to a versioned atomic file. Writes require WriteGameState and the
/// host role; guest copies never see or write the host's table. Persistence is
/// tested by creating two process-like nodes over the same file, and the
/// degrade-to-empty contract is tested with a corrupt file.
/// </summary>
public class ModStateTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TestStateMod StateMod(TestNode node) =>
		(TestStateMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestStateMod);

	private static TestEchoMod EchoMod(TestNode node) =>
		(TestEchoMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestEchoMod);

	[Fact]
	public void Host_CanWriteReadRemoveAndClear()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var state = StateMod(host).Context!.State;

		Assert.True(state.CanWrite);
		Assert.True(state.TrySet("key", [1, 2, 3]));
		Assert.True(state.TryGet("key", out var value));
		Assert.Equal(new byte[] { 1, 2, 3 }, value);

		Assert.True(state.TrySetSchemaVersion(2));
		Assert.Equal(2, state.SchemaVersion);
		Assert.True(state.TryRemove("key"));
		Assert.False(state.TryGet("key", out _));
		Assert.Equal(0, state.Count);

		Assert.True(state.TrySet("a", [1]));
		Assert.True(state.TrySet("b", [2]));
		Assert.True(state.TryClear());
		Assert.Equal(0, state.Count);
		Assert.Empty(state.Keys);
	}

	[Fact]
	public void Guest_CannotWriteOrReadHostState()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var state = StateMod(guest).Context!.State;

		Assert.False(state.CanWrite);
		Assert.False(state.TrySet("key", [1]));
		Assert.False(state.TryGet("key", out _));
		Assert.Equal(0, state.Count);
	}

	[Fact]
	public void HostWithoutWriteGameState_StateWritesAreRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var state = EchoMod(host).Context!.State;

		Assert.False(state.CanWrite, "WriteGameState is required: nothing is implicit.");
		Assert.False(state.TrySet("key", [1]));
		Assert.False(state.TryGet("key", out _));
	}

	[Fact]
	public void ValuesAreDefensivelyCopied_OnWriteAndRead()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var state = StateMod(host).Context!.State;

		var original = new byte[] { 1, 2, 3 };
		Assert.True(state.TrySet("key", original));
		original[0] = 9; // caller mutation must not leak into the store

		Assert.True(state.TryGet("key", out var firstRead));
		firstRead![1] = 8; // caller mutation of the returned copy must not leak either

		Assert.True(state.TryGet("key", out var secondRead));
		Assert.Equal(new byte[] { 1, 2, 3 }, secondRead);
	}

	[Fact]
	public void InvalidKeysAndValues_AreRefusedWithoutSilentTruncation()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var state = StateMod(host).Context!.State;

		Assert.False(state.TrySet("", [1]), "an empty key must be refused.");
		Assert.False(state.TrySet("valid", new byte[64 * 1024 + 1]), "an over-cap value must be refused.");
		Assert.True(state.TrySet("valid", []), "an empty value is legal.");
		Assert.True(state.TryGet("valid", out var empty));
		Assert.Empty(empty!);
	}

	[Fact]
	public void Persistence_SurvivesANewHostProcess()
	{
		var path = Path.Combine(Path.GetTempPath(), "cuo-tests", $"{Guid.NewGuid():N}.mod-state.bin");
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		try
		{
			var clock = new FakeClock();
			var network = new FakeNetwork(clock: clock);
			var steam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
			var host = TestNode.Create(HostId, network, steam, clock, pumpFirstFrame: true, modStateFile: path);
			host.Steam.FireLobbyCreated(LobbyId);
			var state = StateMod(host).Context!.State;
			Assert.True(state.TrySet("persisted", [9, 8, 7]));
			Assert.True(state.TrySetSchemaVersion(4));
			host.Dispose();

			// A fresh node = a fresh process over the same file.
			var clock2 = new FakeClock();
			var network2 = new FakeNetwork(clock: clock2);
			var steam2 = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
			var reopened = TestNode.Create(HostId, network2, steam2, clock2, pumpFirstFrame: true, modStateFile: path);
			reopened.Steam.FireLobbyCreated(LobbyId);
			var reopenedState = StateMod(reopened).Context!.State;
			Assert.Equal(4, reopenedState.SchemaVersion);
			Assert.True(reopenedState.TryGet("persisted", out var value));
			Assert.Equal(new byte[] { 9, 8, 7 }, value);
			reopened.Dispose();
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
	public void CorruptFile_DegradesToEmptyAndNextWriteReplacesIt()
	{
		var path = Path.Combine(Path.GetTempPath(), "cuo-tests", $"{Guid.NewGuid():N}.mod-state.bin");
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		try
		{
			File.WriteAllText(path, "this is not a protobuf mod-state file");

			var clock = new FakeClock();
			var network = new FakeNetwork(clock: clock);
			var steam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
			var host = TestNode.Create(HostId, network, steam, clock, pumpFirstFrame: true, modStateFile: path);
			host.Steam.FireLobbyCreated(LobbyId);
			var state = StateMod(host).Context!.State;

			Assert.Equal(0, state.Count);
			Assert.False(state.TryGet("anything", out _));
			Assert.True(state.TrySet("recovered", [1]), "a write after a corrupt file must replace it with a valid table.");
			host.Dispose();

			Assert.True(File.Exists(path), "the successful write must have replaced the corrupt file.");
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
