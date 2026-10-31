using System;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// What a completer may SEE, and what it may not. A completer is a mod's code running on every keystroke for one command; it is told about that line and the
/// session, never about another mod's surface. "A mod's completer cannot see another mod's facts" is a property of the context it is handed, so this file pins
/// the context's SHAPE as well as the addressing that decides whose completer runs.
/// </summary>
[Trait("Category", "Unit")]
public class ConsoleCompletionScopeTests
{
	private const ulong HostId = 76561198000000001;
	private const ulong GuestId = 76561198000000002;
	private const ulong LobbyId = 10977524123456789;

	/// <summary>
	/// The types a mod-scoped fact travels on. If one of them ever appears on <see cref="ICompletionContext"/>, every completer would be able to read any mod's
	/// state, content or commands — the scope would be a convention instead of a boundary.
	/// </summary>
	private static readonly Type[] ModScopedTypes =
	[
		typeof(IModContext),
		typeof(IModState),
		typeof(IModData),
		typeof(IModConsoleCommands),
		typeof(IModContent),
	];

	[Fact]
	public void ACompleterIsHandedTheLineAndTheSession_AndNoModSurface()
	{
		var members = typeof(ICompletionContext).GetProperties(BindingFlags.Public | BindingFlags.Instance);

		Assert.Equal(
			["Arguments", "CommandName", "ParameterIndex", "Partial", "Session"],
			members.Select(member => member.Name).OrderBy(name => name, StringComparer.Ordinal));

		foreach (var member in members)
		{
			Assert.False(
				IsModScoped(member.PropertyType),
				$"ICompletionContext.{member.Name} carries {member.PropertyType.Name}, so a completer could read another mod's facts");
		}
	}

	/// <summary>Whether a member's type is (or carries) a mod-scoped surface.</summary>
	private static bool IsModScoped(Type type) =>
		ModScopedTypes.Any(scoped => scoped == type || scoped.IsAssignableFrom(type));

	[Fact]
	public void OnlyTheCommandsOwnCompleter_IsAsked_ForItsParameter()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();
		var mod = CommandMod(host);

		// The test mod's cmode completer records the command name it was asked about; the other mod's command must never reach it.
		completion.Suggest("/cmode survivor ");
		completion.Suggest("/cping ");

		var asked = Assert.Single(mod.Completions);
		Assert.Equal("cmode", asked);
	}

	private static TestCommandMod CommandMod(TestNode node) =>
		(TestCommandMod)node.Services.GetRequiredService<Runtime.Session.Mods.ModService>()
			.LoadedMods.Single(m => m is TestCommandMod);
}
