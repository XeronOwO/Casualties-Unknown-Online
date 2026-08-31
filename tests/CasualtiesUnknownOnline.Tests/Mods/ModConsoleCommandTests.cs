using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The local mod console-command surface: registration through
/// <see cref="IModConsoleCommands"/> feeds the same in-game command console as
/// built-ins, commands execute only on the local process, metadata is visible
/// to completion/help, host-only is enforced by role, permissionless mods are
/// refused, duplicate/foreign unregister attempts are safe.
/// </summary>
public class ModConsoleCommandTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TestCommandMod CommandMod(TestNode node) =>
		(TestCommandMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestCommandMod);

	private static TestPermissionlessCommandMod PermissionlessMod(TestNode node) =>
		(TestPermissionlessCommandMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestPermissionlessCommandMod);

	[Fact]
	public void ModConsoleCommand_ExecutesLocallyAndAppearsInCompletion()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var mod = CommandMod(host);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.Contains(completion.Suggest("/cpi"), s => s.Text == "cping");

		Assert.True(console.TryExecute("/cping hello world"));

		Assert.Contains(console.Lines, l => l.Text == "console:hello world");
		Assert.Contains(mod.ConsoleExecutions, e => e.Name == "cping"
			&& e.Arguments.SequenceEqual(["hello", "world"])
			&& e.LocalSteamId == HostId);
	}

	[Fact]
	public void ModConsoleCommand_MetadataIsAvailableToConsoleSurface()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var spec = completion.Commands.Single(c => c.Name == "cping");

		Assert.Equal("Local console echo", spec.Description);
		Assert.Equal("/cping <text>", spec.Usage);
		Assert.Equal(CommandPermission.Anyone, spec.Permission);
		Assert.Contains(CommandArgumentKind.Text, spec.ArgumentKinds);
	}

	[Fact]
	public void ModConsoleCommand_HostOnly_IsRefusedForGuest()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = guest.Services.GetRequiredService<ICommandControl>();

		Assert.False(console.TryExecute("/chost"));
		Assert.Contains(console.Lines, l => l.Kind == ConsoleLineKind.Error && l.Text.Contains("host-only"));
	}

	[Fact]
	public void PermissionlessMod_CannotRegisterConsoleCommands()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var mod = PermissionlessMod(host);

		Assert.False(mod.ConsoleOrdinaryRegistration);
		Assert.False(mod.ConsoleHostRegistration);
		Assert.False(mod.Context!.ConsoleCommands.IsRegistered("cordinary"));
		Assert.DoesNotContain(host.Services.GetRequiredService<ICommandCompletionSource>().Commands,
			c => c.Name == "cordinary");
	}

	[Fact]
	public void DuplicateConsoleCommand_IsRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var mod = CommandMod(host);

		var duplicate = new ModConsoleCommand(
			"cping", "Duplicate", "/cping", CommandPermission.Anyone, [], _ => "duplicate");

		Assert.False(mod.Context!.ConsoleCommands.Register(duplicate));
		Assert.True(mod.Context.ConsoleCommands.IsRegistered("cping"));
		Assert.Contains(host.Services.GetRequiredService<ICommandCompletionSource>().Commands,
			c => c.Name == "cping" && c.Description == "Local console echo");
	}

	[Fact]
	public void Unregister_RemovesOwnConsoleCommand()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var mod = CommandMod(host);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(mod.Context!.ConsoleCommands.Unregister("cping"));
		Assert.False(mod.Context.ConsoleCommands.IsRegistered("cping"));
		Assert.False(console.TryExecute("/cping x"));
		Assert.DoesNotContain(completion.Commands, c => c.Name == "cping");
	}

	[Fact]
	public void Unregister_ForeignOrUnknownName_IsRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var mod = CommandMod(host);

		Assert.False(mod.Context!.ConsoleCommands.Unregister("not-registered"));
		Assert.False(mod.Context.ConsoleCommands.Unregister("help"));
	}
}
