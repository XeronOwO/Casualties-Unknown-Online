using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Command console family: the heal and host-rules command executions through the real services.
/// Split from the former single class so xUnit v2 (serial inside a class)
/// does not serialize this whole surface in one collection.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleCommandTests
{
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
}
