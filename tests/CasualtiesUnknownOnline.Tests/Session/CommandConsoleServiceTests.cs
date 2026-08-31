using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Chat;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
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
}
