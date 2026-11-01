using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The enemy state stream's delta rule. The stream used to carry every enemy on every tick — a flat 4,005 B per frame
/// against ~50 mostly idle crawlers, the host's largest single carrier of a ~200 KB/s send rate with nothing moving
/// (2026-11-02). The rule that replaces it: only what changed, with a full keyframe every second so a dropped
/// unreliable packet cannot leave an enemy stale until it next moves.
/// </summary>
public class EnemyStateDeltaTests
{
	[Fact]
	public void FirstTick_SendsEverything()
	{
		var states = new[] { State(1, 0f, 0f), State(2, 5f, 5f) };
		Assert.Equal(2, EnemyStateDelta.Select(states, new Dictionary<(ulong, uint, byte), WireEnemyStreamState>(), tick: 0).Count);
	}

	[Fact]
	public void UnchangedEnemy_IsNotSentOutsideTheKeyframe()
	{
		var first = State(1, 10f, 20f);
		var sent = new Dictionary<(ulong, uint, byte), WireEnemyStreamState> { [(first.EntityId.Epoch, first.EntityId.Counter, first.EntityId.Generation)] = first };

		Assert.Empty(EnemyStateDelta.Select([State(1, 10f, 20f)], sent, tick: 1));
	}

	[Fact]
	public void MovedEnemy_IsSent()
	{
		var first = State(1, 10f, 20f);
		var sent = new Dictionary<(ulong, uint, byte), WireEnemyStreamState> { [(first.EntityId.Epoch, first.EntityId.Counter, first.EntityId.Generation)] = first };

		Assert.Single(EnemyStateDelta.Select([State(1, 10.5f, 20f)], sent, tick: 1));
	}

	[Fact]
	public void SubEpsilonDrift_IsNotAChange()
	{
		var first = State(1, 10f, 20f);
		var sent = new Dictionary<(ulong, uint, byte), WireEnemyStreamState> { [(first.EntityId.Epoch, first.EntityId.Counter, first.EntityId.Generation)] = first };

		// A hundredth of a unit is below the visible resolution the epsilon encodes.
		Assert.Empty(EnemyStateDelta.Select([State(1, 10.01f, 20.01f)], sent, tick: 1));
	}

	[Fact]
	public void KeyframeTick_SendsEverythingAgain()
	{
		var first = State(1, 10f, 20f);
		var sent = new Dictionary<(ulong, uint, byte), WireEnemyStreamState> { [(first.EntityId.Epoch, first.EntityId.Counter, first.EntityId.Generation)] = first };

		Assert.Single(EnemyStateDelta.Select([State(1, 10f, 20f)], sent, EnemyStateDelta.KeyframeTicks));
	}

	[Fact]
	public void NewEnemy_IsAlwaysSent()
	{
		var first = State(1, 10f, 20f);
		var sent = new Dictionary<(ulong, uint, byte), WireEnemyStreamState> { [(first.EntityId.Epoch, first.EntityId.Counter, first.EntityId.Generation)] = first };

		var selected = EnemyStateDelta.Select([State(1, 10f, 20f), State(2, 0f, 0f)], sent, tick: 1);
		Assert.Single(selected);
		Assert.Equal(2u, selected[0].EntityId.Counter);
	}

	[Fact]
	public void ACrowdOfIdleCrawlers_SendsNothing()
	{
		var crowd = Enumerable.Range(1, 50).Select(i => State(i, i, i)).ToList();
		var sent = crowd.ToDictionary(s => (s.EntityId.Epoch, s.EntityId.Counter, s.EntityId.Generation), s => s);

		Assert.Empty(EnemyStateDelta.Select(crowd, sent, tick: 7));
	}

	private static WireEnemyStreamState State(int id, float x, float y) =>
		new()
		{
			EntityId = new WireEntityId { Counter = (uint)id },
			Position = new WireVector2 { X = x, Y = y },
			Velocity = new WireVector2 { X = 0f, Y = 0f },
			Rotation = 0f,
			Health = 100f,
		};
}
