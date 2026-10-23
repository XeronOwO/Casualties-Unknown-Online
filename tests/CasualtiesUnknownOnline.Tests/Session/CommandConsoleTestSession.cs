using System;
using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Configuration;
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

	internal sealed class StubHostRulesEditor(
		MutableOptionsMonitor<HostRulesOptions>? hostRules = null,
		MutableOptionsMonitor<RespawnOptions>? respawn = null) : IHostRulesEditor
	{
		internal List<(HostRuleId Rule, string Value)> Applied { get; } = [];

		/// <summary>When set, the editor refuses every write with this reason — the composition's own refusal path.</summary>
		internal string? Refusal { get; set; }

		/// <summary>
		/// When set, the write is ACCEPTED but the entry really ends up holding this value — the clamp a
		/// config layer may apply underneath the editor. The command must answer what the entry holds, so
		/// this is what proves the answer is a READ-BACK rather than the value the caller asked for.
		/// </summary>
		internal string? InForceAfterWrite { get; set; }

		public bool TrySet(HostRuleId rule, string value, out string? error)
		{
			if (Refusal is not null)
			{
				error = Refusal;
				return false;
			}

			Applied.Add((rule, value));
			if (hostRules is not null && respawn is not null)
			{
				Write(rule, InForceAfterWrite ?? value, hostRules, respawn);
			}

			error = null;
			return true;
		}

		/// <summary>
		/// The BepInEx write the plugin editor performs, faked at the only seam the test project can reach:
		/// the options monitor the rule surface is composed from. A production write fires the same monitor,
		/// which is what makes the read-back and the announcement edge real in these cases.
		/// </summary>
		private static void Write(HostRuleId rule, string value, MutableOptionsMonitor<HostRulesOptions> hostRules, MutableOptionsMonitor<RespawnOptions> respawn)
		{
			var host = Copy(hostRules.CurrentValue);
			var respawnValues = Copy(respawn.CurrentValue);
			switch (rule)
			{
				case HostRuleId.PvpEnabled:
					host.PvpEnabled = bool.Parse(value);
					break;
				case HostRuleId.AutoContinue:
					host.AutoContinue = bool.Parse(value);
					break;
				case HostRuleId.AllowLateJoin:
					host.AllowLateJoin = bool.Parse(value);
					break;
				case HostRuleId.RemoteBackpackAccess:
					if (!RemoteBackpackAccessText.TryParse(value, out var access))
					{
						throw new FormatException($"'{value}' is not a remote-backpack access level");
					}

					host.RemoteBackpackAccess = access;
					break;
				case HostRuleId.AllowMemberCheatVerbs:
					host.AllowMemberCheatVerbs = bool.Parse(value);
					break;
				case HostRuleId.WidenRunSettings:
					host.WidenRunSettings = bool.Parse(value);
					break;
				case HostRuleId.PiggybackWeightMultiplier:
					host.PiggybackWeightMultiplier = float.Parse(value, CultureInfo.InvariantCulture);
					break;
				case HostRuleId.NativeBindingParity:
					if (!NativeBindingParityText.TryParse(value, out var parity))
					{
						throw new FormatException($"'{value}' is not a native-binding parity level");
					}

					host.NativeBindingParity = parity;
					break;
				case HostRuleId.SaveInventory:
					respawnValues.RespawnKeepInventory = bool.Parse(value);
					break;
				case HostRuleId.ReviveFromTrader:
					respawnValues.ReviveFromTrader = bool.Parse(value);
					break;
				case HostRuleId.ReviveOnNextLevel:
					respawnValues.ReviveOnNextLevel = bool.Parse(value);
					break;
				case HostRuleId.Permadeath:
					respawnValues.Permadeath = bool.Parse(value);
					break;
				case HostRuleId.RadiationLineStopsAboveTheFloor:
					host.RadiationLineStopsAboveTheFloor = bool.Parse(value);
					break;
				case HostRuleId.SpectatorFreeFly:
					host.SpectatorFreeFly = bool.Parse(value);
					break;
				case HostRuleId.SpectateWhileUnconscious:
					host.SpectateWhileUnconscious = bool.Parse(value);
					break;
				case HostRuleId.SpectateWhileWaitingToJoin:
					host.SpectateWhileWaitingToJoin = bool.Parse(value);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(rule), rule, "the test editor has no writer for this rule");
			}

			hostRules.Set(host);
			respawn.Set(respawnValues);
		}

		private static HostRulesOptions Copy(HostRulesOptions source) => new()
		{
			PvpEnabled = source.PvpEnabled,
			AutoContinue = source.AutoContinue,
			AllowLateJoin = source.AllowLateJoin,
			RemoteBackpackAccess = source.RemoteBackpackAccess,
			AllowMemberCheatVerbs = source.AllowMemberCheatVerbs,
			WidenRunSettings = source.WidenRunSettings,
			PiggybackWeightMultiplier = source.PiggybackWeightMultiplier,
			NativeBindingParity = source.NativeBindingParity,
			RadiationLineStopsAboveTheFloor = source.RadiationLineStopsAboveTheFloor,
			SpectatorFreeFly = source.SpectatorFreeFly,
			SpectateWhileUnconscious = source.SpectateWhileUnconscious,
			SpectateWhileWaitingToJoin = source.SpectateWhileWaitingToJoin,
		};

		private static RespawnOptions Copy(RespawnOptions source) => new()
		{
			Permadeath = source.Permadeath,
			ReviveFromTrader = source.ReviveFromTrader,
			ReviveOnNextLevel = source.ReviveOnNextLevel,
			RespawnKeepInventory = source.RespawnKeepInventory,
			RespawnKeepSkills = source.RespawnKeepSkills,
		};
	}
}
