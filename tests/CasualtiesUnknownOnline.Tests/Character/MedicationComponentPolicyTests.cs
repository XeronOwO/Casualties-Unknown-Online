using CasualtiesUnknownOnline.GameAdapter.Character;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Character;

/// <summary>
/// What a receiver must do with a medication component when authoritative state arrives.
/// The defect these cases exist for: the apply side only ever ADDED, so an owner whose
/// component was gone sent the message's defaults and every receiver kept its own copy —
/// stale painkillers, a decaying dose that came back, and a mindwipe that could not be
/// cleared. The three states are named so that "absent" cannot be read as "nothing to do".
/// </summary>
public class MedicationComponentPolicyTests
{
	// The heal's hollow half depends on this one: clearing the debuff means the component goes
	// away, and a receiver that reads `false` as "no news" keeps the player hollow.
	[Fact]
	public void MindwipeAbsentOnTheSender_IsAbsentHere() =>
		Assert.Equal(MedicationComponentPresence.Absent, MedicationComponentPolicy.ForMindwipe(present: false, active: false));

	[Fact]
	public void MindwipePresentButSwitchedOff_IsInactiveRatherThanAbsent()
	{
		// The two are different states: an inactive mindwipe still exists on the body, so
		// removing it here would be the receiver inventing a change the sender did not make.
		Assert.Equal(MedicationComponentPresence.Inactive, MedicationComponentPolicy.ForMindwipe(present: true, active: false));
		Assert.Equal(MedicationComponentPresence.Active, MedicationComponentPolicy.ForMindwipe(present: true, active: true));
	}

	[Fact]
	public void AZeroedDose_MeansTheComponentIsGone()
	{
		Assert.Equal(MedicationComponentPresence.Absent, MedicationComponentPolicy.ForSleepingPills(0f));
		Assert.Equal(MedicationComponentPresence.Active, MedicationComponentPolicy.ForSleepingPills(0.5f));
	}

	[Fact]
	public void AntidepressantsAreAbsentOnlyWhenBothAmountsClear()
	{
		// Either value alone keeps the component alive: the game evolves one into the other.
		Assert.Equal(MedicationComponentPresence.Absent, MedicationComponentPolicy.ForAntidepressants(0f, 0f));
		Assert.Equal(MedicationComponentPresence.Active, MedicationComponentPolicy.ForAntidepressants(0.4f, 0f));
		Assert.Equal(MedicationComponentPresence.Active, MedicationComponentPolicy.ForAntidepressants(0f, 0.4f));
	}

	[Fact]
	public void PainkillersAreAbsentOnlyWhenEveryFieldIsClear()
	{
		// Five fields, and tolerance/reception outlive the opiate itself — a body with only
		// tolerance left still has the component.
		Assert.Equal(MedicationComponentPresence.Absent, MedicationComponentPolicy.ForPainkillers(0f, 0f, 0f, 0f, 0f));
		Assert.Equal(MedicationComponentPresence.Active, MedicationComponentPolicy.ForPainkillers(0f, 30f, 0f, 0f, 0f));
		Assert.Equal(MedicationComponentPresence.Active, MedicationComponentPolicy.ForPainkillers(1f, 0f, 0f, 0f, 0f));
	}
}
