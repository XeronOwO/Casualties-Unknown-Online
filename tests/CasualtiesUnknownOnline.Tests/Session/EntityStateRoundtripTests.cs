using System;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The player stream wire roundtrip: every pose flag (the packed Flags byte) and
/// the extended flag bits survive <see cref="WirePlayerStreamState"/> / the
/// stream mapper exactly — a flag dropped or cross-wired between bits is a
/// silent presentation bug (the peer's clone poses wrong). The table is the
/// single source of truth for the bit → property classification; a new bit in
/// ApplyTo without a table entry (or an entry without a bit) fails the
/// completeness guard.
/// </summary>
public class EntityStateRoundtripTests
{
	/// <summary>The Flags byte's bit → property mapping (8 pose bits, frozen — WirePlayerStreamState).</summary>
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
			new WirePlayerStreamState { Flags = bit }.ApplyTo(entity);

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
		new WirePlayerStreamState { ExtendedFlags = 0x01u }.ApplyTo(entity);

		Assert.True(entity.IsAttacking);
	}

	[Fact]
	public void ExtendedFlags_Zero_ClearsIsAttacking()
	{
		var entity = NewEntity(isAttacking: true);
		new WirePlayerStreamState { ExtendedFlags = 0u }.ApplyTo(entity);

		Assert.False(entity.IsAttacking);
	}

	[Fact]
	public void IsAttacking_PublishesToExtendedFlags_Bit0x01()
	{
		var entity = NewEntity(isAttacking: true);

		Assert.Equal(0x01u, entity.ToWirePlayerStreamState().ExtendedFlags);
	}

	[Fact]
	public void IsAttacking_FullRoundtrip()
	{
		var wire = NewEntity(isAttacking: true).ToWirePlayerStreamState();

		var target = NewEntity();
		wire.ApplyTo(target);

		Assert.True(target.IsAttacking);
	}

	[Fact]
	public void ExtendedFlags_SlidingLeft_RoundtripsIntoEntity()
	{
		var entity = NewEntity();
		new WirePlayerStreamState { ExtendedFlags = 0x02u }.ApplyTo(entity);

		Assert.True(entity.SlidingLeft);
		Assert.False(entity.SlidingRight);
	}

	[Fact]
	public void ExtendedFlags_SlidingRight_RoundtripsIntoEntity()
	{
		var entity = NewEntity();
		new WirePlayerStreamState { ExtendedFlags = 0x04u }.ApplyTo(entity);

		Assert.False(entity.SlidingLeft);
		Assert.True(entity.SlidingRight);
	}

	[Fact]
	public void SlidingFlags_PublishToExtendedFlags_Bits0x02And0x04()
	{
		var entity = NewEntity();
		entity.SlidingLeft = true;
		entity.SlidingRight = true;

		var wire = entity.ToWirePlayerStreamState();

		Assert.Equal(0x06u, wire.ExtendedFlags & 0x06u);
	}

	[Fact]
	public void SlidingFlags_FullRoundtrip()
	{
		var source = NewEntity();
		source.SlidingLeft = true;
		source.SlidingRight = true;

		var wire = source.ToWirePlayerStreamState();
		var target = NewEntity();
		wire.ApplyTo(target);

		Assert.True(target.SlidingLeft);
		Assert.True(target.SlidingRight);
	}

	[Fact]
	public void SwingSeq_AppliesIntoEntity_AndRoundTripsBackToWire()
	{
		var entity = NewEntity();
		new WirePlayerStreamState { SwingSeq = 200 }.ApplyTo(entity);

		Assert.Equal(200, entity.SwingSeq);
		Assert.Equal(200, entity.ToWirePlayerStreamState().SwingSeq);
	}

	[Fact]
	public void SwingSeq_DefaultsToZero_NeverSwinged()
	{
		var entity = NewEntity();

		Assert.Equal(0, entity.ToWirePlayerStreamState().SwingSeq);
	}

	[Fact]
	public void WorkoutType_AppliesIntoEntity_AndRoundTripsBackToWire()
	{
		var entity = NewEntity();
		new WirePlayerStreamState { WorkoutType = 2 }.ApplyTo(entity);

		Assert.Equal(2, entity.WorkoutType);
		Assert.Equal(2, entity.ToWirePlayerStreamState().WorkoutType);
	}

	[Fact]
	public void WorkoutType_DefaultsToZero_NotExercising()
	{
		var entity = NewEntity();

		Assert.Equal(0, entity.ToWirePlayerStreamState().WorkoutType);
	}

	[Fact]
	public void WorkoutType_ZeroClearsThePreviousValue()
	{
		var entity = NewEntity();
		entity.WorkoutType = 3;
		new WirePlayerStreamState { WorkoutType = 0 }.ApplyTo(entity);

		Assert.Equal(0, entity.WorkoutType);
	}

	[Fact]
	public void NapVariant_AppliesIntoEntity_AndRoundTripsBackToWire()
	{
		var entity = NewEntity();
		new WirePlayerStreamState { NapVariant = 1 }.ApplyTo(entity);

		Assert.Equal(1, entity.NapVariant);
		Assert.Equal(1, entity.ToWirePlayerStreamState().NapVariant);
	}

	[Fact]
	public void NapVariant_DefaultsToZero_StandardLayDown()
	{
		var entity = NewEntity();

		Assert.Equal(0, entity.ToWirePlayerStreamState().NapVariant);
	}

	[Fact]
	public void DogShakeIntensity_AppliesIntoEntity_AndRoundTripsBackToWire()
	{
		var entity = NewEntity();
		new WirePlayerStreamState { DogShakeIntensity = 0.175f }.ApplyTo(entity);

		Assert.Equal(0.175f, entity.DogShakeIntensity);
		Assert.Equal(0.175f, entity.ToWirePlayerStreamState().DogShakeIntensity);
	}

	[Fact]
	public void DogShakeIntensity_DefaultsToZero_NoShake()
	{
		var entity = NewEntity();

		Assert.Equal(0f, entity.ToWirePlayerStreamState().DogShakeIntensity);
	}

	[Fact]
	public void LimbPoses_RoundtripIntoEntityAndBackToWire()
	{
		var source = NewEntity();
		source.LimbPoses =
		[
			new PlayerLimbPose { Index = 0, LocalPosition = new NetVector2(1.25f, -2.5f), RotationZ = 45f },
			new PlayerLimbPose { Index = 3, LocalPosition = new NetVector2(-0.75f, 3.5f), RotationZ = -120f },
		];

		var wire = source.ToWirePlayerStreamState();
		var target = NewEntity();
		wire.ApplyTo(target);

		Assert.NotNull(target.LimbPoses);
		Assert.Equal(2, target.LimbPoses!.Count);
		Assert.Equal(0, target.LimbPoses[0].Index);
		Assert.Equal(1.25f, target.LimbPoses[0].LocalPosition.X);
		Assert.Equal(-2.5f, target.LimbPoses[0].LocalPosition.Y);
		Assert.Equal(45f, target.LimbPoses[0].RotationZ);
		Assert.Equal(3, target.LimbPoses[1].Index);
		Assert.Equal(-0.75f, target.LimbPoses[1].LocalPosition.X);
		Assert.Equal(3.5f, target.LimbPoses[1].LocalPosition.Y);
		Assert.Equal(-120f, target.LimbPoses[1].RotationZ);
	}

	[Fact]
	public void LimbPoses_DefaultToNull_AndNullClearsPreviousValue()
	{
		var entity = NewEntity();
		entity.LimbPoses =
		[
			new PlayerLimbPose { Index = 0, LocalPosition = new NetVector2(1f, 2f), RotationZ = 3f },
		];

		Assert.NotNull(entity.ToWirePlayerStreamState().LimbPoses);

		var wire = new WirePlayerStreamState();
		wire.ApplyTo(entity);

		Assert.Null(entity.LimbPoses);
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

		var wire = entity.ToWirePlayerStreamState();
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

		var wire = entity.ToWirePlayerStreamState();

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
