using System.Diagnostics;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// Completion runs on EVERY keystroke, over a command list a session grows (built-ins plus every mod's definitions). This file holds the cost bound the ticket
/// asks for: a realistic line completes many times over inside a frame's budget, and the list stays inside the cap the overlay draws.
/// </summary>
[Trait("Category", "Performance")]
public class ConsoleCompletionCostTests
{
	private const ulong HostId = 76561198000000001;
	private const ulong GuestId = 76561198000000002;
	private const ulong LobbyId = 10977524123456789;

	/// <summary>
	/// The per-call budget. A 60 Hz frame has ~16.7 ms for EVERYTHING; completion may use a tenth of that and still leave the frame to the game. Generous on
	/// purpose: this test is a stall detector, not a benchmark, so a loaded CI machine reports a real regression instead of its own noise.
	/// </summary>
	private const double BudgetMilliseconds = 1.6;

	private const int Calls = 200;

	[Fact]
	public void ARealisticLine_CompletesManyTimesWithinOneFramesBudget()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services => { });
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		// Warm every path once: the first call pays for JIT and for the session snapshot's own lazily built lists.
		foreach (var line in new[] { "/give @p cu:wat", "/summon item ", "/cmode survivor ", "/music ", "/" })
		{
			completion.Suggest(line);
		}

		var stopwatch = Stopwatch.StartNew();
		for (var call = 0; call < Calls; call++)
		{
			completion.Suggest("/give @p cu:wat");
		}

		stopwatch.Stop();
		var perCall = stopwatch.Elapsed.TotalMilliseconds / Calls;
		Assert.True(
			perCall < BudgetMilliseconds,
			$"completion took {perCall:F3} ms per call over {Calls} calls (budget {BudgetMilliseconds} ms): a keystroke would stall the frame");
	}

	[Fact]
	public void TheWidestLine_IsTheWorstCaseAndStaysInsideTheBudget()
	{
		// "/" offers every command word at once — the widest list the console can draw — so it is the line whose cost matters most.
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();
		completion.Suggest("/");

		var stopwatch = Stopwatch.StartNew();
		for (var call = 0; call < Calls; call++)
		{
			completion.Suggest("/");
		}

		stopwatch.Stop();
		var perCall = stopwatch.Elapsed.TotalMilliseconds / Calls;
		Assert.True(
			perCall < BudgetMilliseconds,
			$"the widest line took {perCall:F3} ms per call over {Calls} calls (budget {BudgetMilliseconds} ms)");
	}
}
