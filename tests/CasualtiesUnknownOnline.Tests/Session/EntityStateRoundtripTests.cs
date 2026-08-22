using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The entity-state wire roundtrip: every pose flag (the packed Flags byte) and
/// the extended flag bits survive <see cref="EntityStateMsg.ApplyTo"/> exactly
/// — a flag dropped or cross-wired between bits is a silent presentation bug
/// (the peer's clone poses wrong). The table is the single source of truth for
/// the bit → property classification; a new bit in ApplyTo without a table
/// entry (or an entry without a bit) fails the completeness guard.
/// </summary>
public class EntityStateRoundtripTests
{
	/// <summary>The Flags byte's bit → property mapping (8 pose bits, frozen — EntityStateMsg.cs).</summary>
	private static readonly (byte Bit, Func<PlayerEntity, bool> Get)[] FlagBits =
	[
		(0x01, e => e.IsRight),
		(0x02, e => e.Standing),
		(0x04, e => e.Alive),
		(0x08, e => e.Conscious),
		(0x10, e => e.Crouching),
		(0x20, e => e.Sitting),
		(0x40, e => e.Sleeping),
		(0x80, e => e.Climbing),
	];

	private static PlayerEntity NewEntity(bool isAttacking = false) =>
		new(steamId: 0, entityId: default, isLocal: false) { IsAttacking = isAttacking };

	[Fact]
	public void Flags_EveryBitRoundtrips_AndTableIsComplete()
	{
		// Completeness guard: 8 distinct non-zero bits whose union is the full
		// Flags byte — a new pose flag added to ApplyTo without a table entry,
		// or a duplicate/missing bit, fails here.
		var or = 0;
		foreach (var (bit, _) in FlagBits)
		{
			Assert.NotEqual(0, bit);
			or |= bit;
		}

		Assert.Equal(8, FlagBits.Length);
		Assert.Equal(8, FlagBits.Select(b => b.Bit).Distinct().Count());
		Assert.Equal(0xFF, or);

		foreach (var (bit, get) in FlagBits)
		{
			var entity = NewEntity();
			new EntityStateMsg { Flags = bit }.ApplyTo(entity);

			Assert.True(get(entity), $"bit 0x{bit:X2} must set its property");
			foreach (var (otherBit, otherGet) in FlagBits)
			{
				if (otherBit == bit)
				{
					continue;
				}

				Assert.False(otherGet(entity), $"bit 0x{bit:X2} must not set the 0x{otherBit:X2} property");
			}
		}
	}

	[Fact]
	public void ExtendedFlags_IsAttacking_Roundtrips_IntoEntity()
	{
		var entity = NewEntity();
		new EntityStateMsg { ExtendedFlags = 0x01u }.ApplyTo(entity);

		Assert.True(entity.IsAttacking);
	}

	[Fact]
	public void ExtendedFlags_Zero_ClearsIsAttacking()
	{
		var entity = NewEntity(isAttacking: true);
		new EntityStateMsg { ExtendedFlags = 0u }.ApplyTo(entity);

		Assert.False(entity.IsAttacking);
	}

	[Fact]
	public void IsAttacking_PublishesToExtendedFlags_Bit0x01()
	{
		var entity = NewEntity(isAttacking: true);

		Assert.Equal(0x01u, entity.ToEntityStateMsg().ExtendedFlags);
	}

	[Fact]
	public void IsAttacking_FullRoundtrip()
	{
		var wire = NewEntity(isAttacking: true).ToEntityStateMsg();

		var target = NewEntity();
		wire.ApplyTo(target);

		Assert.True(target.IsAttacking);
	}

	[Fact]
	public void SwingSeq_AppliesIntoEntity_AndRoundTripsBackToWire()
	{
		var entity = NewEntity();
		new EntityStateMsg { SwingSeq = 200 }.ApplyTo(entity);

		Assert.Equal(200, entity.SwingSeq);
		Assert.Equal(200, entity.ToEntityStateMsg().SwingSeq);
	}

	[Fact]
	public void SwingSeq_DefaultsToZero_NeverSwinged()
	{
		var entity = NewEntity();

		Assert.Equal(0, entity.ToEntityStateMsg().SwingSeq);
	}

	[Fact]
	public void GazeOverrideAndEyeScare_Roundtrip()
	{
		var entity = NewEntity();
		entity.LookOverridePos = new NetVector2(12.5f, -3.25f);
		entity.LookOverrideTime = 0.75f;
		entity.EyeScareTime = 1.5f;
		entity.EyePanicTime = 0.6f;
		entity.EyeCloseTime = 2.25f;

		var wire = entity.ToEntityStateMsg();
		var target = NewEntity();
		wire.ApplyTo(target);

		Assert.True(target.LookOverridePos.HasValue);
		Assert.Equal(12.5f, target.LookOverridePos!.Value.X);
		Assert.Equal(-3.25f, target.LookOverridePos.Value.Y);
		Assert.Equal(0.75f, target.LookOverrideTime);
		Assert.Equal(1.5f, target.EyeScareTime);
		Assert.Equal(0.6f, target.EyePanicTime);
		Assert.Equal(2.25f, target.EyeCloseTime);
	}

	[Fact]
	public void GazeOverride_DefaultsToNull_AndTimersToZero()
	{
		var entity = NewEntity();

		var wire = entity.ToEntityStateMsg();

		Assert.Null(wire.LookOverridePos);
		Assert.Equal(0f, wire.LookOverrideTime);
		Assert.Equal(0f, wire.EyeScareTime);
		Assert.Equal(0f, wire.EyePanicTime);
		Assert.Equal(0f, wire.EyeCloseTime);

		var target = NewEntity();
		wire.ApplyTo(target);

		Assert.False(target.LookOverridePos.HasValue);
		Assert.Equal(0f, target.LookOverrideTime);
		Assert.Equal(0f, target.EyeScareTime);
		Assert.Equal(0f, target.EyePanicTime);
		Assert.Equal(0f, target.EyeCloseTime);
	}
}
