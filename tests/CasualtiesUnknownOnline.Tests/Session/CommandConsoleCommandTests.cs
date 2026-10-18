using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Command console family: the host-rules command executions through the real services.
/// The <c>/heal</c> cheat verb's own matrix lives in
/// <see cref="CommandConsoleHealResetTests"/> and <see cref="CommandConsoleMemberHealResetTests"/>.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleCommandTests
{
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
