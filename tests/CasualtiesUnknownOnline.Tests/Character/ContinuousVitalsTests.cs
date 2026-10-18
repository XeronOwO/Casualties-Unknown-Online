using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Character;

/// <summary>
/// The snapshot half of the host-owned vitals seam: the write of the clone's values, the
/// comparison that recognizes an authoritative write, and the report filter that keeps a member
/// from stating them. The Game Adapter's body half (read/copy/restore on the clone) is pinned by
/// the patch/stage source pins; the real-store round trip is <see cref="PlayerVitalsOwnershipTests"/>.
/// </summary>
public class ContinuousVitalsTests
{
	[Fact]
	public void Write_PutsTheClonesValuesIntoTheSnapshotByLimbIndex()
	{
		var data = Snapshot(blood: 0f, total: 0f, bleeds: [(0, 0f), (1, 0f)]);
		var bleeds = new[] { 3.5f, 7f, 99f };

		ContinuousVitals.Write(data, bloodVolume: 55f, totalBleedSpeed: 0.25f, bleeds);

		Assert.Equal(55f, data.Health!.BloodVolume, 3);
		Assert.Equal(0.25f, data.Health.TotalBleedSpeed, 3);
		Assert.Equal(3.5f, data.Limbs[0].BleedAmount, 3);
		Assert.Equal(7f, data.Limbs[1].BleedAmount, 3);
	}

	[Fact]
	public void Write_IgnoresALimbIndexTheBufferDoesNotCover()
	{
		var data = Snapshot(blood: 0f, total: 0f, bleeds: [(0, 1f), (9, 2f)]);

		ContinuousVitals.Write(data, 1f, 1f, [4f]);

		Assert.Equal(4f, data.Limbs[0].BleedAmount, 3);
		Assert.Equal(2f, data.Limbs[1].BleedAmount, 3);
	}

	[Fact]
	public void Differs_SeesEachOwnedValueBeyondTheEpsilon()
	{
		var data = Snapshot(blood: 80f, total: 0.5f, bleeds: [(0, 5f)]);

		Assert.False(ContinuousVitals.Differs(data, 80f, 0.5f, [5f]));
		Assert.True(ContinuousVitals.Differs(data, 80.01f, 0.5f, [5f]));
		Assert.True(ContinuousVitals.Differs(data, 80f, 0.51f, [5f]));
		Assert.True(ContinuousVitals.Differs(data, 80f, 0.5f, [5.01f]));
	}

	[Fact]
	public void Differs_IgnoresADifferenceInsideTheEpsilon()
	{
		var data = Snapshot(blood: 80f, total: 0.5f, bleeds: [(0, 5f)]);

		Assert.False(ContinuousVitals.Differs(data, 80.0004f, 0.5004f, [5.0004f]));
	}

	[Fact]
	public void Differs_IgnoresALimbTheBufferDoesNotCover()
	{
		var data = Snapshot(blood: 80f, total: 0.5f, bleeds: [(0, 5f), (9, 100f)]);

		Assert.False(ContinuousVitals.Differs(data, 80f, 0.5f, [5f]));
	}

	[Fact]
	public void Preserve_KeepsAllThreeOwnedValues()
	{
		var previous = Snapshot(blood: 80f, total: 0.5f, bleeds: [(0, 5f)]);
		var next = Snapshot(blood: 10f, total: 9f, bleeds: [(0, 0f)]);

		ContinuousVitals.Preserve(previous, next);

		Assert.Equal(80f, next.Health!.BloodVolume, 3);
		Assert.Equal(0.5f, next.Health.TotalBleedSpeed, 3);
		Assert.Equal(5f, next.Limbs[0].BleedAmount, 3);
	}

	[Fact]
	public void Preserve_ZeroesALimbThePreviousSnapshotDidNotCarry()
	{
		var previous = Snapshot(blood: 80f, total: 0f, bleeds: [(0, 5f)]);
		var next = Snapshot(blood: 80f, total: 0f, bleeds: [(0, 5f), (1, 4f)]);

		ContinuousVitals.Preserve(previous, next);

		Assert.Equal(0f, next.Limbs[1].BleedAmount, 3);
	}

	private static CharacterDataMsg Snapshot(float blood, float total, (int Index, float Bleed)[] bleeds)
	{
		var data = new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { BloodVolume = blood, TotalBleedSpeed = total },
		};

		foreach (var (index, bleed) in bleeds)
		{
			data.Limbs.Add(new CharacterLimbMsg { Index = index, BleedAmount = bleed, SkinHealth = 50f });
		}

		return data;
	}
}
