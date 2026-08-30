using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Session;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

public class PlayerProjectionTests
{
	private const ulong HostId = 1001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void HostPublishLocalState_CommitsPlayerKernelStatus()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var entities = host.Services.GetRequiredService<IEntitySyncControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

		entities.PublishLocalState(
			new NetVector2(1, 2),
			new NetVector2(3, 4),
			new NetVector2(0, 0),
			isRight: true,
			standing: true,
			alive: false,
			conscious: false,
			crouching: false);

		var player = Assert.Single(authority.QueryPlayers()!.Players);
		Assert.Equal(HostId, player.SteamId);
		Assert.False(player.Alive);
		Assert.False(player.Conscious);
	}

	[Fact]
	public void HostSaveCharacterData_CommitsPlayerKernelLimbFacts()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

		characters.SaveHostCharacterData(new CharacterDataMsg
		{
			OwnerSteamId = HostId,
			Health = new CharacterHealthMsg { Alive = true, Conscious = true },
			Limbs =
			[
				new CharacterLimbMsg { Index = 0, Broken = true, Dismembered = false, Dislocated = false, Splinted = true, IsHead = true },
				new CharacterLimbMsg { Index = 1, Broken = false, Dismembered = true, IsVital = true },
			],
		});

		var player = Assert.Single(authority.QueryPlayers()!.Players);
		Assert.Equal(2, player.LimbFacts.Count);
		var head = player.LimbFacts.Single(l => l.Index == 0);
		Assert.True(head.Broken);
		Assert.True(head.Splinted);
		Assert.True(head.IsHead);
		var torso = player.LimbFacts.Single(l => l.Index == 1);
		Assert.True(torso.Dismembered);
		Assert.True(torso.IsVital);
	}

	[Fact]
	public void LimbStateEvent_CommitsPlayerKernelLimbFacts()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

		characters.ApplyLimbStateEvent(new LimbStateEventMsg
		{
			OwnerSteamId = HostId,
			Limbs =
			[
				new CharacterLimbMsg { Index = 2, Broken = false, Dismembered = false, Dislocated = true, IsVital = true },
			],
		});

		var player = Assert.Single(authority.QueryPlayers()!.Players);
		var limb = Assert.Single(player.LimbFacts);
		Assert.True(limb.Dislocated);
		Assert.True(limb.IsVital);
	}

	[Fact]
	public void HostSaveCharacterData_CommitsPlayerKernelBodyTerminalFacts()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

		characters.SaveHostCharacterData(new CharacterDataMsg
		{
			OwnerSteamId = HostId,
			Health = new CharacterHealthMsg
			{
				Alive = true,
				Conscious = true,
				Disfigured = true,
				EyeGone = true,
				BothEyesGone = false,
				HasPulmonaryEmbolism = true,
				TriedRollingLastStand = false,
				SuccesfullyRolledLastStand = true,
				UsedNeuralBooster = true,
				FibrillationForced = false,
				MindwipeScriptPresent = true,
				MindwipeScriptActive = true,
			},
		});

		var player = Assert.Single(authority.QueryPlayers()!.Players);
		Assert.NotNull(player.Body);
		Assert.True(player.Body!.Disfigured);
		Assert.True(player.Body!.EyeGone);
		Assert.True(player.Body!.HasPulmonaryEmbolism);
		Assert.True(player.Body!.SuccesfullyRolledLastStand);
		Assert.True(player.Body!.UsedNeuralBooster);
		Assert.True(player.Body!.MindwipeScriptPresent);
		Assert.True(player.Body!.MindwipeScriptActive);
	}

	[Fact]
	public void HostSaveCharacterData_CommitsPlayerKernelSkills()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

		characters.SaveHostCharacterData(new CharacterDataMsg
		{
			OwnerSteamId = HostId,
			Health = new CharacterHealthMsg { Alive = true, Conscious = true },
			Skills = new CharacterSkillsMsg
			{
				Strength = 15,
				Resistance = 12,
				Intelligence = 9,
				ExpStrength = 3.5f,
				ExpResistance = 2.25f,
				ExpIntelligence = 1.75f,
			},
		});

		var player = Assert.Single(authority.QueryPlayers()!.Players);
		Assert.NotNull(player.Skills);
		Assert.Equal(15, player.Skills!.Strength);
		Assert.Equal(12, player.Skills.Resistance);
		Assert.Equal(9, player.Skills.Intelligence);
		Assert.Equal(3.5f, player.Skills.ExpStrength);
		Assert.Equal(2.25f, player.Skills.ExpResistance);
		Assert.Equal(1.75f, player.Skills.ExpIntelligence);
	}

	[Fact]
	public void LimbStateEvent_CommitsPlayerKernelBodyTerminalFacts()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

		characters.ApplyLimbStateEvent(new LimbStateEventMsg
		{
			OwnerSteamId = HostId,
			Limbs =
			[
				new CharacterLimbMsg { Index = 0, Broken = true, IsHead = true },
			],
			Health = new CharacterHealthMsg
			{
				Disfigured = false,
				EyeGone = true,
				BothEyesGone = true,
				HasPulmonaryEmbolism = false,
				TriedRollingLastStand = true,
				SuccesfullyRolledLastStand = false,
				UsedNeuralBooster = false,
				FibrillationForced = true,
				MindwipeScriptPresent = false,
				MindwipeScriptActive = true,
			},
		});

		var player = Assert.Single(authority.QueryPlayers()!.Players);
		Assert.NotNull(player.Body);
		Assert.True(player.Body!.EyeGone);
		Assert.True(player.Body!.BothEyesGone);
		Assert.True(player.Body!.TriedRollingLastStand);
		Assert.True(player.Body!.FibrillationForced);
		Assert.True(player.Body!.MindwipeScriptActive);
	}
}
