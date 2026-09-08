using CasualtiesUnknownOnline.Runtime.Session.Chat;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Command console family: command dispatch, help and the plain-text chat echo.
/// Split from the former single class so xUnit v2 (serial inside a class)
/// does not serialize this whole surface in one collection.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleDispatchTests
{
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
}
