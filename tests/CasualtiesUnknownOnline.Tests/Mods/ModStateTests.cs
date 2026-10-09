using System;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The phase-4 mod-state surface: a host-only, per-mod key/value store of
/// <see cref="ModValue"/> persisted to a versioned atomic file as each value's
/// canonical encoding. Writes require WriteGameState and the host role; guest
/// copies never see or write the host's table. Persistence is tested by
/// creating two process-like nodes over the same file, and the degrade contract
/// is tested with a corrupt file, a stored byte string that is not a value, and
/// a file from the byte-shaped version 1.
/// </summary>
[Trait("Category", "Integration")]
public class ModStateTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TestStateMod StateMod(TestNode node) =>
		(TestStateMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestStateMod);

	private static TestEchoMod EchoMod(TestNode node) =>
		(TestEchoMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestEchoMod);

	/// <summary>A fresh host process over the given mod-state file, past its lobby setup. <paramref name="extraRegistrations"/> may swap a service — a suite's log recorder, for one.</summary>
	private static TestNode CreateHostOver(string path, Action<IServiceCollection>? extraRegistrations = null)
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var steam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var host = TestNode.Create(
			HostId, network, steam, clock, pumpFirstFrame: true, modStateFile: path, extraRegistrations: extraRegistrations);
		host.Steam.FireLobbyCreated(LobbyId);
		return host;
	}

	private static string NewStateFilePath() =>
		Path.Combine(Path.GetTempPath(), "cuo-tests", $"{Guid.NewGuid():N}.mod-state.bin");

	/// <summary>Writes a mod-state file directly, so a case can present a file this build did not produce.</summary>
	private static void WriteStateFile(string path, ModStateFile file)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		using var stream = File.Create(path);
		Serializer.Serialize(stream, file);
	}

	/// <summary>The value's canonical encoding — the production encoder, so a hand-built file carries what the framework would write.</summary>
	private static byte[] Encoded(ModValue value)
	{
		Assert.True(ModValueCodec.TryEncode(value, ModStatePolicy.MaxValueBytes, out var encoded, out var refusal), refusal);
		return encoded;
	}

	[Fact]
	public void Host_CanWriteReadRemoveAndClear()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var state = StateMod(host).Context!.State;

		Assert.True(state.CanWrite);
		Assert.True(state.TrySet("key", ModValues.Ints(1, 2, 3)));
		Assert.True(state.TryGet("key", out var value));
		Assert.Equal(ModValues.Ints(1, 2, 3), value);

		Assert.True(state.TrySetSchemaVersion(2));
		Assert.Equal(2, state.SchemaVersion);
		Assert.True(state.TryRemove("key"));
		Assert.False(state.TryGet("key", out _));
		Assert.Equal(0, state.Count);

		Assert.True(state.TrySet("a", ModValue.Integer(1)));
		Assert.True(state.TrySet("b", ModValue.Integer(2)));
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
		Assert.False(state.TrySet("key", ModValue.Integer(1)));
		Assert.False(state.TryGet("key", out _));
		Assert.Equal(0, state.Count);
	}

	[Fact]
	public void HostWithoutWriteGameState_StateWritesAreRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var state = EchoMod(host).Context!.State;

		Assert.False(state.CanWrite, "WriteGameState is required: nothing is implicit.");
		Assert.False(state.TrySet("key", ModValue.Integer(1)));
		Assert.False(state.TryGet("key", out _));
	}

	[Fact]
	public void AValueIsImmutable_TheCallersOwnArrayCannotReachTheStoredTable()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var state = StateMod(host).Context!.State;

		var original = new byte[] { 1, 2, 3 };
		var stored = ModValue.Binary(original);
		Assert.True(state.TrySet("key", stored));
		original[0] = 9; // the value copied the bytes: the caller's later write cannot reach the table

		Assert.True(state.TryGet("key", out var firstRead));
		Assert.Equal(stored, firstRead);
		Assert.True(firstRead!.TryGetBinary(out var bytes));
		Assert.Equal([1, 2, 3], bytes.ToArray());

		Assert.True(state.TryGet("key", out var secondRead));
		Assert.Equal(stored, secondRead);
	}

	[Fact]
	public void InvalidKeysAndValues_AreRefusedWithoutSilentTruncation()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var state = StateMod(host).Context!.State;

		Assert.False(state.TrySet("", ModValue.Integer(1)), "an empty key must be refused.");
		Assert.False(state.TrySet("valid", ModValues.OverCap()), "a value that cannot be encoded inside the cap must be refused.");
		Assert.False(state.TrySet("valid", null!), "a null value must be refused rather than stored.");
		Assert.True(state.TrySet("valid", ModValue.Boolean(false)), "a minimal value is legal.");
		Assert.True(state.TryGet("valid", out var stored));
		Assert.True(stored!.TryGetBoolean(out var flag));
		Assert.False(flag);

		// The rail is inclusive: a value whose encoding is exactly the cap is inside it.
		Assert.True(state.TrySet("valid", ModValues.AtTheRail()), "a value that encodes to exactly the cap is legal.");
	}

	[Fact]
	public void Persistence_SurvivesANewHostProcess()
	{
		var path = NewStateFilePath();
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		try
		{
			var host = CreateHostOver(path);
			var state = StateMod(host).Context!.State;
			var loadout = ModValue.Map(
				("weapon", ModValue.Text("rifle")),
				("mags", ModValue.List(ModValue.Integer(3), ModValue.Integer(1))),
				("sealed", ModValue.Binary(new byte[] { 7, 7 })),
				("saved", ModValue.Number(1.5)));
			Assert.True(state.TrySet("persisted", loadout));
			Assert.True(state.TrySetSchemaVersion(4));
			host.Dispose();

			// A fresh node = a fresh process over the same file.
			var reopened = CreateHostOver(path);
			var reopenedState = StateMod(reopened).Context!.State;
			Assert.Equal(4, reopenedState.SchemaVersion);
			Assert.True(reopenedState.TryGet("persisted", out var value));
			Assert.Equal(loadout, value);
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
		var path = NewStateFilePath();
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		try
		{
			File.WriteAllText(path, "this is not a protobuf mod-state file");

			var host = CreateHostOver(path);
			var state = StateMod(host).Context!.State;

			Assert.Equal(0, state.Count);
			Assert.False(state.TryGet("anything", out _));
			Assert.True(state.TrySet("recovered", ModValue.Integer(1)), "a write after a corrupt file must replace it with a valid table.");
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

	[Fact]
	public void AStoredByteStringThatIsNotAValue_IsDroppedByNameAndTheRestOfTheTableLoads()
	{
		var path = NewStateFilePath();
		try
		{
			WriteStateFile(path, new ModStateFile
			{
				Entries =
				[
					new ModStateFile.Entry
					{
						ModId = "test.state",
						ModVersion = "1.0.0",
						SchemaVersion = 2,
						States =
						[
							new ModStateFile.StateEntry { Key = "kept", Value = Encoded(ModValue.Map(("hp", ModValue.Integer(7)))) },
							// 0x00 is the model's `false`; the second byte is left over, and a payload that is
							// not exactly one value is not a value.
							new ModStateFile.StateEntry { Key = "legacy", Value = [0x00, 0x01] },
						],
					},
				],
			});

			var recorder = new RecordingLoggerFactory();
			var host = CreateHostOver(path, s => s.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(recorder)));
			var state = StateMod(host).Context!.State;

			Assert.Equal(1, state.Count);
			Assert.True(state.TryGet("kept", out var kept));
			Assert.Equal(ModValue.Map(("hp", ModValue.Integer(7))), kept);
			Assert.False(state.TryGet("legacy", out _), "a stored byte string the framework cannot read is dropped, not guessed at.");
			Assert.Equal(2, state.SchemaVersion);
			Assert.Contains(
				recorder.Messages(LogLevel.Warning, nameof(ModService)),
				message => message.Contains("test.state/legacy", StringComparison.Ordinal)
					&& message.Contains("dropped", StringComparison.Ordinal));
			host.Dispose();
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
	public void AVersionOneFile_IsRefusedWholeRatherThanReinterpreted()
	{
		var path = NewStateFilePath();
		try
		{
			// Version 1 stored whatever bytes the mod chose, so `0x00` there meant the mod's
			// own byte and means the value `false` here: the two cannot be told apart, and the
			// file's own contract for a version this build does not read is empty + warn.
			WriteStateFile(path, new ModStateFile
			{
				Version = 1,
				Entries =
				[
					new ModStateFile.Entry
					{
						ModId = "test.state",
						ModVersion = "1.0.0",
						SchemaVersion = 1,
						States = [new ModStateFile.StateEntry { Key = "legacy", Value = [0x00] }],
					},
				],
			});

			var host = CreateHostOver(path);
			var state = StateMod(host).Context!.State;

			Assert.Equal(0, state.Count);
			Assert.False(state.TryGet("legacy", out _));
			host.Dispose();
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
