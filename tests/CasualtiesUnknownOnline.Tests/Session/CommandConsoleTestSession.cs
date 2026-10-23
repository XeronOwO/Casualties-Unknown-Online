using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Permissions;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The stateless session helpers shared by the command-console test classes: the handshaken host+guest pair, the entity seed and the character snapshots. Every test builds its own session — this class holds no state.
/// </summary>
internal static class CommandConsoleTestSession
{
	internal const ulong HostId = 1001;

	internal const ulong GuestId = 2001;

	internal const ulong LobbyId = 9001;

	internal static (TestNode Host, TestNode Guest) CreateSession(Action<IServiceCollection>? extraRegistrations = null, NetMsg? swallowHostToGuest = null)
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations, swallowHostToGuest: swallowHostToGuest);
		MarkInWorld(host);
		MarkInWorld(guest);
		return (host, guest);
	}

	internal static void MarkInWorld(TestNode node) =>
		node.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

	/// <summary>
	/// The host's cheat-roster grant for one guest, ANNOUNCED and asserted: the WHO half
	/// of the two cheat rulings every member console cheat verb needs (the switch is the
	/// other). The announce half is what separates it from <see cref="ItemSimWorld"/>'s
	/// grant helper, which deliberately does not announce. The assert is a PRECONDITION,
	/// so a suite that forgot it fails on the setup rather than on the verdict it means
	/// to test.
	/// </summary>
	internal static void GrantAndAnnounceCheatVerbs(TestNode host, TestNode guest)
	{
		var roster = host.Services.GetRequiredService<CheatPermissions>();
		Assert.True(roster.Grant(guest.SteamId, out var refusal), $"the host grants the member the cheat verbs: {refusal}");
		host.Services.GetRequiredService<ICheatPermissionAnnouncement>().AnnounceAll();
		Assert.Contains(guest.SteamId, guest.Services.GetRequiredService<AnnouncedCheatPermissions>().Current ?? []);
	}

	internal static void SeedHostEntities(TestNode host, ulong guestId, float guestX, float guestY = 0f)
	{
		var entities = host.Services.GetRequiredService<IEntitySyncControl>();
		entities.PublishLocalState(
			new NetVector2(0f, 0f),
			new NetVector2(1f, 1f),
			NetVector2.Zero,
			isRight: true,
			standing: true,
			alive: true,
			conscious: true,
			crouching: false);
		entities.ProcessPlayerJoin(new PlayerJoinMsg
		{
			HostSteamId = HostId,
			GuestSteamId = guestId,
			HostPosition = new NetVector2Msg(0f, 0f),
			GuestPosition = new NetVector2Msg(guestX, guestY),
		});
		var guestEntity = entities.GetRemotePlayer(guestId);
		if (guestEntity is not null)
		{
			guestEntity.Position = new NetVector2(guestX, guestY);
			guestEntity.Standing = true;
			guestEntity.Alive = true;
			guestEntity.Conscious = true;
		}
	}

	internal static CharacterItemMsg Item(ulong instanceId, string itemId = "bandage", int slot = 0) => new()
	{
		InstanceId = instanceId,
		ItemId = itemId,
		SlotIndex = slot,
		Condition = 1f,
	};

	internal static CharacterDataMsg Snapshot(ulong owner, bool conscious, params CharacterItemMsg[] items) => new()
	{
		OwnerSteamId = owner,
		Items = [.. items],
		Health = new CharacterHealthMsg
		{
			Alive = true,
			Conscious = conscious,
			BrainHealth = conscious ? 80f : 5f,
		},
	};

	internal static CharacterDataMsg SnapshotWithLimbs(ulong owner, bool conscious, bool alive = true, params CharacterItemMsg[] items)
	{
		var data = Snapshot(owner, conscious, items);
		data.Health!.Alive = alive;
		data.Limbs =
		[
			new CharacterLimbMsg { Index = 0, SkinHealth = 50f, MuscleHealth = 50f },
			new CharacterLimbMsg { Index = 1, SkinHealth = 20f, MuscleHealth = 30f },
			new CharacterLimbMsg { Index = 2, SkinHealth = 80f, MuscleHealth = 80f },
		];
		return data;
	}

	internal sealed class StubHostRulesEditor : IHostRulesEditor
	{
		internal List<(HostRuleId Rule, string Value)> Applied { get; } = [];

		/// <summary>When set, the editor refuses every write with this reason — the composition's own refusal path.</summary>
		internal string? Refusal { get; set; }

		public bool TrySet(HostRuleId rule, string value, out string? error)
		{
			if (Refusal is not null)
			{
				error = Refusal;
				return false;
			}

			Applied.Add((rule, value));
			error = null;
			return true;
		}
	}
}
