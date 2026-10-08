using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The limb-tool family's one admission rule
/// (<c>mod-cross-player-native-semantics</c>, Part B's last chain): the item's own
/// data decides the family, and the chains that have not migrated keep their claims.
/// <para>
/// What this pins: the RULE — the exclusion list, one line per unmigrated chain, and
/// the fact that a claimed item is refused even though its own data would admit it
/// (every id below is listed as a limb action item by the fake, so an exclusion that
/// stopped being asked would show up here as a pass rather than as silence).
/// What it does NOT pin: the game's own content. A readable gate cannot read a game
/// assembly, so the production answer is pinned by
/// <c>LimbToolChainGateTests.TheFamilysAnswer_IsTheGamesOwnItemRegistryAndItsOwnLimbAction</c>
/// and the delegate census is the self-check's.
/// </para>
/// </summary>
public class LimbToolAdmissionTests
{
	[Fact]
	public void Admission_AcceptsAToolTheDeletedCatalogCarried()
	{
		Assert.True(LimbToolAdmission.IsLimbTool(FakeLimbUseSemantics.Instance, FakeSolidFoodSemantics.Instance, "medicalsuture"));
		Assert.True(LimbToolAdmission.IsLimbTool(FakeLimbUseSemantics.Instance, FakeSolidFoodSemantics.Instance, "splint"));
	}

	[Fact]
	public void Admission_AcceptsAVanillaLimbActionTheDeletedCatalogNeverCarried()
	{
		// The migration's own point: these carry a useLimbAction in the game's data and
		// were never in the nine-row table, so they could be carried, dropped and saved
		// but never applied to a teammate.
		Assert.True(LimbToolAdmission.IsLimbTool(FakeLimbUseSemantics.Instance, FakeSolidFoodSemantics.Instance, "roselight"));
		Assert.True(LimbToolAdmission.IsLimbTool(FakeLimbUseSemantics.Instance, FakeSolidFoodSemantics.Instance, "plasmacutter"));
	}

	[Fact]
	public void Admission_RefusesAnItemWhoseOwnActionAlsoFeedsABody()
	{
		// bulbskin's useAction drinks 4.5 (Item.cs:2534-2544) and xalorissponge's eats 8
		// (Item.cs:2571-2578) while BOTH also carry a limb action, and the host's chain
		// asks the solid-food rule first. Without this exclusion a wound-view gesture on
		// one of them would make the treated player EAT it, because the request cannot
		// say which of the two actions the gesture meant.
		Assert.False(LimbToolAdmission.IsLimbTool(FakeLimbUseSemantics.Instance, FakeSolidFoodSemantics.Instance, "bulbskin"));
		Assert.False(LimbToolAdmission.IsLimbTool(FakeLimbUseSemantics.Instance, FakeSolidFoodSemantics.Instance, "xalorissponge"));

		// ...and the family they DO belong to still answers for them.
		Assert.Equal(
			SolidFoodVerdict.Eats,
			SolidFoodAdmission.Classify(FakeSolidFoodSemantics.Instance, "bulbskin"));
	}

	[Fact]
	public void Admission_RefusesEveryItemAnUnmigratedChainClaims()
	{
		// The heal chain's items (the dressing family and the one-shot adhesive
		// bandage), the wound-view bandage minigame's items (musharm is both), the
		// other-medical sessions' tools, and the shared shrapnel session's tweezers.
		foreach (var claimed in new[]
		{
			"bandage", "adhesivebandage", "musharm",
			"aed", "manualdefibrillator", "machete", "wrench",
			"tweezers",
		})
		{
			Assert.False(LimbToolAdmission.IsLimbTool(FakeLimbUseSemantics.Instance, FakeSolidFoodSemantics.Instance, claimed));
		}
	}

	[Fact]
	public void Admission_RefusesAnItemTheSeamDoesNotCallALimbActionItem()
	{
		// The seam is the game's own answer, and it is asked FIRST: a liquid carrier
		// (the two liquid chains' business), a plain item and an unknown id all fall out
		// before any claim is consulted.
		Assert.False(LimbToolAdmission.IsLimbTool(FakeLimbUseSemantics.Instance, FakeSolidFoodSemantics.Instance, "syringe"));
		Assert.False(LimbToolAdmission.IsLimbTool(FakeLimbUseSemantics.Instance, FakeSolidFoodSemantics.Instance, "bread"));
		Assert.False(LimbToolAdmission.IsLimbTool(FakeLimbUseSemantics.Instance, FakeSolidFoodSemantics.Instance, "mysterytool"));
		Assert.False(LimbToolAdmission.IsLimbTool(FakeLimbUseSemantics.Instance, FakeSolidFoodSemantics.Instance, ""));
	}
}
