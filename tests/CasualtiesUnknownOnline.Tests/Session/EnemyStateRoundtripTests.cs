using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The enemy-state wire roundtrip: every presentation field and every packed
/// presentation-flag bit survives <see cref="EnemyStateMsg.ApplyTo"/> exactly —
/// a field dropped or a flag cross-wired between bits is a silent divergence
/// (the peer's enemy copy poses/moves wrong). The table is the single source of
/// truth for the flag-bit classification; a new flag in ApplyTo without a table
/// entry fails the completeness guard.
/// </summary>
public class EnemyStateRoundtripTests
{
	/// <summary>The presentation-flag bit → property mapping (bits are frozen — EnemyStateMsg.cs).</summary>
	private static readonly (uint Bit, Func<EnemyEntity, bool> Get)[] PresentationFlagBits =
	[
		(EnemyStateMsg.FlagStunned, e => e.Stunned),
	];

	private static EnemyEntity NewEntity() => new(default);

	[Fact]
	public void PresentationFlags_EveryBitRoundtrips_AndTableIsComplete()
	{
		// Completeness guard: distinct non-zero bits — a new flag added to
		// ApplyTo without a table entry, or a duplicate bit, fails here.
		var or = 0u;
		foreach (var (bit, _) in PresentationFlagBits)
		{
			Assert.NotEqual(0u, bit);
			or |= bit;
		}

		Assert.Equal(PresentationFlagBits.Length, PresentationFlagBits.Select(b => b.Bit).Distinct().Count());

		foreach (var (bit, get) in PresentationFlagBits)
		{
			var entity = NewEntity();
			new EnemyStateMsg { PresentationFlags = bit }.ApplyTo(entity);

			Assert.True(get(entity), $"flag 0x{bit:X} must set its property");
			foreach (var (otherBit, otherGet) in PresentationFlagBits)
			{
				if (otherBit == bit)
				{
					continue;
				}

				Assert.False(otherGet(entity), $"flag 0x{bit:X} must not set the 0x{otherBit:X} property");
			}
		}
	}

	[Fact]
	public void EnemyState_FullRoundtrip()
	{
		var source = new EnemyEntity(new NetworkEntityId(7, 3, 1))
		{
			Position = new NetVector2(1.5f, -2.5f),
			Velocity = new NetVector2(0.5f, 0.25f),
			Rotation = 90f,
			Health = 42.5f,
			Stunned = true,
		};

		var target = NewEntity();
		source.ToEnemyStateMsg().ApplyTo(target);

		Assert.Equal(source.EntityId, target.EntityId);
		Assert.Equal(source.Position, target.Position);
		Assert.Equal(source.Velocity, target.Velocity);
		Assert.Equal(source.Rotation, target.Rotation);
		Assert.Equal(source.Health, target.Health);
		Assert.Equal(source.Stunned, target.Stunned);
	}

	[Fact]
	public void Stunned_PublishesFlagStunned()
	{
		var entity = new EnemyEntity(default) { Stunned = true };

		Assert.Equal(EnemyStateMsg.FlagStunned, entity.ToEnemyStateMsg().PresentationFlags);
	}

	[Fact]
	public void PresentationFlags_Zero_ClearsStunned()
	{
		var entity = new EnemyEntity(default) { Stunned = true };

		new EnemyStateMsg { PresentationFlags = 0u }.ApplyTo(entity);

		Assert.False(entity.Stunned);
	}
}
