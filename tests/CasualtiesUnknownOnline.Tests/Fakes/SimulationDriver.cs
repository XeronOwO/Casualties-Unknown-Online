using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The phase-2 simulation pump: advances the SHARED virtual clock (the
/// network's delivery schedule and every node's domain services read the same
/// FakeClock), then runs every node's full ICuoService Update loop — one
/// frame at a time, exactly like the production Plugin. Tests express
/// scenarios as time-ordered operations (actions + faults) and converge on
/// invariants via <see cref="TickUntil"/>; a scenario that never converges
/// fails the test (the pump is bounded, not silent).
/// </summary>
internal sealed class SimulationDriver
{
	private readonly FakeClock _clock;
	private readonly FakeNetwork _network;
	private readonly TestNode[] _nodes;

	internal SimulationDriver(FakeClock clock, FakeNetwork network, params TestNode[] nodes)
	{
		_clock = clock;
		_network = network;
		_nodes = nodes;
	}

	internal FakeClock Clock => _clock;

	internal FakeNetwork Network => _network;

	internal IReadOnlyList<TestNode> Nodes => _nodes;

	internal long NowMs => _clock.NowMs;

	/// <summary>One frame: advance the virtual clock (delivering every message that
	/// came due), then run all nodes' Update loops in registration order.</summary>
	internal void Tick(long ms)
	{
		_network.Advance(ms);
		foreach (var node in _nodes)
		{
			node.Update();
		}
	}

	/// <summary>Pump <paramref name="stepMs"/>-frames until <paramref name="done"/>
	/// holds or <paramref name="maxMs"/> of virtual time elapsed — a scenario that
	/// never converges throws (the "convergence" property is itself asserted).</summary>
	internal void TickUntil(Func<bool> done, long maxMs, long stepMs = 33)
	{
		var deadline = _clock.NowMs + maxMs;
		while (!done())
		{
			if (_clock.NowMs >= deadline)
			{
				throw new InvalidOperationException($"simulation did not converge within {maxMs} ms (clock at {_clock.NowMs} ms)");
			}

			Tick(stepMs);
		}
	}

	internal TestNode Node(ulong steamId) => _nodes.Single(n => n.SteamId == steamId);
}
