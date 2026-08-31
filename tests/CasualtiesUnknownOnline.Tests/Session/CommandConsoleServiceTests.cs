using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Chat;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The local command/chat console: slash-command parsing, role permission
/// gates, chat forwarding and the host-only admin commands through the real
/// session/ban services. No new wire message is involved.
/// </summary>
public class CommandConsoleServiceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void Help_ReturnsAvailableCommands()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/help"));
		Assert.Contains(console.Lines, l => l.Text.Contains("/help"));
		Assert.Contains(console.Lines, l => l.Text.Contains("/kick"));
	}

	[Fact]
	public void UnknownCommand_AddsErrorLine()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.False(console.TryExecute("/no-such-command"));
		Assert.Contains(console.Lines, l => l.Kind == ConsoleLineKind.Error && l.Text.Contains("Unknown command"));
	}

	[Fact]
	public void PlainText_SendsChatAndEchoesInConsole()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();
		var chat = host.Services.GetRequiredService<IChatControl>();

		Assert.True(console.TryExecute("hello from console"));
		Assert.Contains(chat.Recent, l => l.Text == "hello from console");
		Assert.Contains(console.Lines, l => l.Text == "You: hello from console");
	}

	[Fact]
	public void HostOnlyCommand_IsRefusedForGuest()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = guest.Services.GetRequiredService<ICommandControl>();

		Assert.False(console.TryExecute("/kick 1"));
		Assert.Contains(console.Lines, l => l.Kind == ConsoleLineKind.Error && l.Text.Contains("host-only"));
	}

	[Fact]
	public void HostKick_RemovesMember()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute($"/kick {GuestId}"));
		Assert.DoesNotContain(host.Session.Members, m => m.SteamId == GuestId);
		Assert.Contains(console.Lines, l => l.Text.Contains("Kicked member"));
	}

	[Fact]
	public void HostBan_And_Unban_RoundTrip()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();
		var hostBan = host.Services.GetRequiredService<IHostBanService>();

		Assert.True(console.TryExecute($"/ban {GuestId}"));
		Assert.True(hostBan.IsBanned(GuestId));
		Assert.Contains(console.Lines, l => l.Text.Contains("Banned member"));

		Assert.True(console.TryExecute($"/unban {GuestId}"));
		Assert.False(hostBan.IsBanned(GuestId));
		Assert.Contains(console.Lines, l => l.Text.Contains("Unbanned"));
	}

	[Fact]
	public void Clear_EmptiesOutput()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();
		console.TryExecute("/help");

		Assert.True(console.Lines.Count > 0);
		console.Clear();
		Assert.Empty(console.Lines);
	}

	[Fact]
	public void Suggest_ReturnsCommandNamesForPrefix()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest("/k");

		Assert.Contains(suggestions, s => s.Text == "kick");
	}

	[Fact]
	public void Suggest_IncludesDescriptionForCommand()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest("/k");

		Assert.Contains(suggestions, s => s.Text == "kick" && !string.IsNullOrWhiteSpace(s.Description));
	}

	[Fact]
	public void Suggest_ReturnsMemberIdForKickArgument()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest($"/kick {GuestId}");

		Assert.Contains(suggestions, s => s.Text == GuestId.ToString());
	}

	[Fact]
	public void Help_WithCommandName_ShowsUsage()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/help kick"));
		Assert.Contains(console.Lines, l => l.Text.Contains("Usage: /kick"));
	}

	[Fact]
	public void GetHint_ReturnsUsageForKnownCommand()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var hint = completion.GetHint("/kick");

		Assert.Contains("/kick <steamId|displayName>", hint);
	}

	[Fact]
	public void ArgumentSuggestions_SelectorKind_ReturnsSelectors()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var suggestions = host.Services.GetRequiredService<ICommandArgumentSuggestions>();

		var matches = suggestions.Suggest(CommandArgumentKind.Selector, "@a");

		Assert.Contains(matches, s => s.Text == "@a" && s.Description == "All players");
	}

	[Fact]
	public void ArgumentSuggestions_JsonKind_ReturnsTemplates()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var suggestions = host.Services.GetRequiredService<ICommandArgumentSuggestions>();

		var matches = suggestions.Suggest(CommandArgumentKind.Json, "{\"k");

		Assert.Contains(matches, s => s.Text == "{\"key\": \"value\"}");
	}

	[Fact]
	public void Suggest_ForHealSelectorArgument_ReturnsSelectors()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest("/heal @");

		Assert.Contains(suggestions, s => s.Text == "@a");
		Assert.Contains(suggestions, s => s.Text == "@p");
	}

	[Fact]
	public void GetHint_ForHeal_ShowsSelectorUsage()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var hint = completion.GetHint("/heal");

		Assert.Contains("/heal <selector>", hint);
	}

	[Fact]
	public void Heal_WithSelector_SendsRequestToGuest()
	{
		var (host, guest) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(77, "bandage", slot: 0)));
		characters.SaveCharacterData(GuestId, SnapshotWithLimbs(GuestId, conscious: false));
		SeedHostEntities(host, GuestId, guestX: 10f);

		var console = host.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute("/heal @p"));

		var healed = characters.GetSavedCharacter(GuestId)!;
		Assert.True(healed.Limbs[1].SkinHealAmount > 0f);
		Assert.Contains(console.Lines, l => l.Text.Contains("Sent heal request to 1 player(s): 2001"));
	}

	[Fact]
	public void Heal_UnknownSelector_AddsNoMatchLine()
	{
		var (host, _) = CreateSession();
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/heal @z"));
		Assert.Contains(console.Lines, l => l.Text.Contains("No players match selector '@z'"));
	}

	[Fact]
	public void Heal_WithoutSelector_ShowsUsage()
	{
		var (host, _) = CreateSession();
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/heal"));
		Assert.Contains(console.Lines, l => l.Text.Contains("Usage: /heal <selector>"));
	}

	[Fact]
	public void Suggest_ForHostRulesJsonArgument_ReturnsTemplates()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest("/hostrules {");

		Assert.Contains(suggestions, s => s.Text == "{\"key\": \"value\"}");
	}

	[Fact]
	public void GetHint_ForHostRules_ShowsJsonUsage()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var hint = completion.GetHint("/hostrules");

		Assert.Contains("/hostrules <json>", hint);
	}

	[Fact]
	public void HostRules_WithJson_UpdatesEditor()
	{
		var editor = new StubHostRulesEditor();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<IHostRulesEditor>(editor)));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/hostrules {\"AllowLateJoin\": false}"));
		Assert.Contains(("AllowLateJoin", "false"), editor.Applied);
		Assert.Contains(console.Lines, l => l.Text.Contains("Updated 1 host rule(s)"));
	}

	[Fact]
	public void HostRules_MalformedJson_AddsError()
	{
		var editor = new StubHostRulesEditor();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<IHostRulesEditor>(editor)));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/hostrules {\"AllowLateJoin\": false"));
		Assert.Contains(console.Lines, l => l.Kind == ConsoleLineKind.Success && l.Text.Contains("Unterminated JSON object"));
	}

	[Fact]
	public void HostRules_WithoutJson_ShowsUsage()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/hostrules"));
		Assert.Contains(console.Lines, l => l.Text.Contains("Usage: /hostrules <json>"));
	}

	private static (TestNode Host, TestNode Guest) CreateSession()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		MarkInWorld(host);
		MarkInWorld(guest);
		return (host, guest);
	}

	private static void MarkInWorld(TestNode node) =>
		node.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

	private static void SeedHostEntities(TestNode host, ulong guestId, float guestX, float guestY = 0f)
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

	private static CharacterItemMsg Item(ulong instanceId, string itemId = "bandage", int slot = 0) => new()
	{
		InstanceId = instanceId,
		ItemId = itemId,
		SlotIndex = slot,
		Condition = 1f,
	};

	private static CharacterDataMsg Snapshot(ulong owner, bool conscious, params CharacterItemMsg[] items) => new()
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

	private static CharacterDataMsg SnapshotWithLimbs(ulong owner, bool conscious, bool alive = true, params CharacterItemMsg[] items)
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

	private sealed class StubHostRulesEditor : IHostRulesEditor
	{
		internal List<(string Property, string Value)> Applied { get; } = [];

		public bool TrySet(string property, string value, out string? error)
		{
			Applied.Add((property, value));
			error = null;
			return true;
		}
	}
}
