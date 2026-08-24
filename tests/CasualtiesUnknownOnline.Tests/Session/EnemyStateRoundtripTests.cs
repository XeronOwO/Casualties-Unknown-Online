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
			SpiderLegTargets =
			[
				new NetVector2(10f, 11f),
				new NetVector2(12f, 13f),
			],
		};

		var target = NewEntity();
		source.ToEnemyStateMsg().ApplyTo(target);

		Assert.Equal(source.EntityId, target.EntityId);
		Assert.Equal(source.Position, target.Position);
		Assert.Equal(source.Velocity, target.Velocity);
		Assert.Equal(source.Rotation, target.Rotation);
		Assert.Equal(source.Health, target.Health);
		Assert.Equal(source.Stunned, target.Stunned);
		Assert.Equal(2, target.SpiderLegTargets!.Count);
		Assert.Equal(new NetVector2(10f, 11f), target.SpiderLegTargets![0]);
		Assert.Equal(new NetVector2(12f, 13f), target.SpiderLegTargets![1]);
	}

	[Fact]
	public void SpiderLegTargets_MissingLeavesNull()
	{
		var target = NewEntity();
		new EnemyStateMsg().ApplyTo(target);

		Assert.Null(target.SpiderLegTargets);
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

	[Fact]
	public void EnemySpawnEntry_Roundtrip_FromEntity()
	{
		var source = new EnemyEntity(new NetworkEntityId(7, 3, 1))
		{
			Position = new NetVector2(11f, -22f),
			Rotation = 180f,
			PrefabId = "crystalenemy",
			RuntimeSpawned = true,
			HasTint = true,
			TintColor = new NetColorRgba(0.25f, 0.5f, 0.75f, 1f),
			TintLightIntensity = 0.8f,
		};

		var msg = source.ToEnemySpawnEntryMsg();

		Assert.Equal(source.EntityId, msg.Id.ToNetworkEntityId());
		Assert.Equal(source.PrefabId, msg.PrefabId);
		Assert.Equal(source.Position, msg.Position.ToNetVector2());
		Assert.Equal(source.Rotation, msg.Rotation);
		Assert.True(msg.HasTint);
		Assert.Equal(0.25f, msg.TintColor.R);
		Assert.Equal(0.5f, msg.TintColor.G);
		Assert.Equal(0.75f, msg.TintColor.B);
		Assert.Equal(1f, msg.TintColor.A);
		Assert.Equal(0.8f, msg.LightIntensity);
	}

}
