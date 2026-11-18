using System;
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
[Collection(PerformanceCollection.Name)]
public class ConsoleCompletionCostTests
{
	private const ulong HostId = 76561198000000001;
	private const ulong GuestId = 76561198000000002;
	private const ulong LobbyId = 10977524123456789;

	/// <summary>
	/// The per-call budget. A 60 Hz frame has ~16.7 ms for EVERYTHING; completion may use a tenth of that and still leave the frame to the game. Generous on
	/// purpose: this test is a stall detector, not a benchmark, so a loaded CI machine reports a real regression instead of its own noise.
	///
	/// The measured quantity is the calling thread's CPU time, not elapsed time: a full test run puts several assemblies on the
	/// machine at once and a wall-clock reading of this loop then reports the other processes' load (measured 2026-11-18: 6.091
	/// ms per call while the same test passed alone in 1 s). CPU time is what the frame actually spends.
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

		var perCall = PerCallCpuMilliseconds(() => completion.Suggest("/give @p cu:wat"));
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

		var perCall = PerCallCpuMilliseconds(() => completion.Suggest("/"));
		Assert.True(
			perCall < BudgetMilliseconds,
			$"the widest line took {perCall:F3} ms per call over {Calls} calls (budget {BudgetMilliseconds} ms)");
	}

	/// <summary>Runs <paramref name="work"/> <see cref="Calls"/> times and returns the calling process's CPU milliseconds per call.</summary>
	private static double PerCallCpuMilliseconds(Action work)
	{
		var before = Process.GetCurrentProcess().TotalProcessorTime;
		for (var call = 0; call < Calls; call++)
		{
			work();
		}

		var after = Process.GetCurrentProcess().TotalProcessorTime;
		return (after - before).TotalMilliseconds / Calls;
	}
}
