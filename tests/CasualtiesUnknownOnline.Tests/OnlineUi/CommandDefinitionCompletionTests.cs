using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// A mod's definition answers its own completion: the parameter's completer is asked with the arguments already typed (so argument N can depend on N-1), and a
/// completer that throws is isolated instead of taking the console down. The framework still owns the list's keys and its authority filter — these cases judge
/// only what the definition contributes.
/// </summary>
public sealed class CommandDefinitionCompletionTests
{
	private const ulong HostId = 90000000000000101;
	private const ulong GuestId = 90000000000000102;
	private const long LobbyId = 90000000000000103;

	[Fact]
	public void AParametersCompleter_AnswersWithItsOwnWords()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest("/cmode ").Select(suggestion => suggestion.Text).ToList();

		Assert.Contains("survivor", suggestions);
		Assert.Contains("spectator", suggestions);
	}

	[Fact]
	public void TheCompleter_SeesTheArgumentAlreadyTyped()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		// The second parameter lists what the FIRST one implies; a completer blind to the earlier argument would answer both lists here.
		var survivor = completion.Suggest("/cmode survivor ").Select(suggestion => suggestion.Text).ToList();
		var spectator = completion.Suggest("/cmode spectator ").Select(suggestion => suggestion.Text).ToList();

		Assert.Contains("calm", survivor);
		Assert.DoesNotContain("calm", spectator);
		Assert.Contains("fast", spectator);
	}

	[Fact]
	public void ACompleterThatThrows_OffersNothing()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		Assert.Empty(completion.Suggest("/cthrow "));
	}

	[Fact]
	public void AnAlias_IsOfferedBesideItsCommand()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest("/cm").Select(suggestion => suggestion.Text).ToList();

		Assert.Contains("cm", suggestions);
		Assert.Contains("cmode", suggestions);
	}
}
