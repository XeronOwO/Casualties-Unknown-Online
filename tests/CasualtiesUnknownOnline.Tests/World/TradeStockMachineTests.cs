using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The host trader-state machine (TradeStockMachine): the trader-side lines of
/// the game's interaction methods (TraderScript.cs), reproduced as pure state
/// transitions — random values are explicit inputs, the acting side's
/// player-side effects are already done and never replayed. Every acceptance /
/// rejection / penalty branch is locked here.
/// </summary>
public class TradeStockMachineTests
{
	private static TradeStockState NewStock() => new()
	{
		Reputation = 50f,
		Hostility = 0f,
		ValueGiven = 100f,
		TotalValueGiven = 0f,
		FreeAmount = 1,
		FreeDressing = false,
		DidHug = false,
		StartedConvo = false,
		DidMove = false,
		HaggleAmount = 0f,
		Character = 0,
		BuildHealth = 300f,
		MinHugReputation = 60f,
		Items =
		[
			new TraderItemMsg { Id = "water", Value = 10, Preference = TradeStockMachine.PreferenceIndifferent },
		],
	};

	// ---- MeetPlayer (TraderScript.cs:107-154) ----

	[Fact]
	public void MeetPlayer_RerollsReputation_StartsConvo()
	{
		var state = NewStock();

		var result = TradeStockMachine.MeetPlayer(state, repRoll: 100f, repOffset: 10f, repScale: 1f, repPostOffset: -5f, flags: 0, bandageValue: 15);

		Assert.True(result.StartedConvo);
		Assert.Equal(105f, result.Reputation); // (100 + 10) × 1 - 5
		Assert.Equal(0f, result.Hostility);
		Assert.False(result.FreeDressing);
		Assert.Single(result.Items);
	}

	[Fact]
	public void MeetPlayer_HasGun_AddsHostility()
	{
		var state = NewStock();

		var result = TradeStockMachine.MeetPlayer(state, repRoll: 100f, repOffset: 0f, repScale: 1f, repPostOffset: 0f, flags: TraderActionMsg.FlagHasGun, bandageValue: 15);

		Assert.Equal(50f, result.Hostility);
	}

	[Fact]
	public void MeetPlayer_Bleeding_AddsBandageAndSorts()
	{
		var state = NewStock() with { Items = [new TraderItemMsg { Id = "knife", Value = 5, Preference = 2 }] }; // WantsKeep sorts last

		var result = TradeStockMachine.MeetPlayer(state, repRoll: 100f, repOffset: 0f, repScale: 1f, repPostOffset: 0f, flags: TraderActionMsg.FlagBleeding, bandageValue: 15);

		Assert.True(result.FreeDressing);
		Assert.Equal(2, result.Items.Count);
		Assert.True("bandage" == result.Items[0].Id, "the bandage (Indifferent=1) sorts first in the ascending preference order");
		Assert.Equal(TradeStockMachine.PreferenceIndifferent, result.Items[0].Preference);
		Assert.Equal(15, result.Items[0].Value);
		Assert.True("knife" == result.Items[1].Id, "WantsKeep=2 sorts last");
	}

	// ---- TryPurchase (TraderScript.cs:747-804) ----

	[Fact]
	public void Purchase_BuildTooDamaged_Rejected_NoPenalty()
	{
		var state = NewStock() with { BuildHealth = 150f };

		var (accepted, result) = TradeStockMachine.TryPurchase(state, "water", price: 10);

		Assert.False(accepted);
		Assert.True(50f == result.Reputation, "the build gate refuses BEFORE the refusal penalty");
		Assert.Single(result.Items);
	}

	[Fact]
	public void Purchase_UnknownItem_Rejected()
	{
		var state = NewStock();

		var (accepted, result) = TradeStockMachine.TryPurchase(state, "gun", price: 10);

		Assert.False(accepted);
		Assert.Equal(50f, result.Reputation);
	}

	[Fact]
	public void Purchase_NotEnoughValue_Rejected_WithPenalty()
	{
		var state = NewStock() with { ValueGiven = 5f };

		var (accepted, result) = TradeStockMachine.TryPurchase(state, "water", price: 10);

		Assert.False(accepted);
		Assert.True(48f == result.Reputation, "the game's refusal penalty (TraderScript.cs:800)");
		Assert.True(5f == result.ValueGiven, "no charge on a refused purchase");
	}

	[Fact]
	public void Purchase_Success_ChargesAndRemoves()
	{
		var state = NewStock() with { FreeDressing = true };

		var (accepted, result) = TradeStockMachine.TryPurchase(state, "water", price: 10);

		Assert.True(accepted);
		Assert.Equal(90f, result.ValueGiven);
		Assert.Empty(result.Items);
		Assert.False(result.FreeDressing);
		Assert.True(54f == result.Reputation, "Indifferent + 4");
	}

	[Fact]
	public void Purchase_WantsTrade_AddsSevenReputation()
	{
		var state = NewStock() with { Items = [new TraderItemMsg { Id = "water", Value = 10, Preference = TradeStockMachine.PreferenceWantsTrade }] };

		var (accepted, result) = TradeStockMachine.TryPurchase(state, "water", price: 10);

		Assert.True(accepted);
		Assert.Equal(57f, result.Reputation);
	}

	[Fact]
	public void Purchase_FreeAmount_Consumed()
	{
		var state = NewStock(); // FreeAmount = 1

		var (accepted, result) = TradeStockMachine.TryPurchase(state, "water", price: 10);

		Assert.True(accepted);
		Assert.Equal(0, result.FreeAmount);
	}

	[Fact]
	public void Purchase_FreeAmountZero_NotNegative()
	{
		var state = NewStock() with { FreeAmount = 0 };

		var (accepted, result) = TradeStockMachine.TryPurchase(state, "water", price: 10);

		Assert.True(accepted);
		Assert.Equal(0, result.FreeAmount);
	}

	[Fact]
	public void Purchase_FreeItem_NoReputationBonus()
	{
		var state = NewStock(); // price 0 — the free dressing item

		var (accepted, result) = TradeStockMachine.TryPurchase(state, "water", price: 0);

		Assert.True(accepted);
		Assert.True(50f == result.Reputation, "no preference bonus for a free item");
	}

	[Fact]
	public void Purchase_DuplicateListing_RemovesOneOnly()
	{
		// A trader may list the same id twice — the game removes the sold entry
		// by reference, a duplicate stays for a second sale.
		var state = NewStock() with
		{
			Items =
			[
				new TraderItemMsg { Id = "water", Value = 10, Preference = TradeStockMachine.PreferenceIndifferent },
				new TraderItemMsg { Id = "water", Value = 10, Preference = TradeStockMachine.PreferenceIndifferent },
			],
		};

		var (accepted, result) = TradeStockMachine.TryPurchase(state, "water", price: 10);

		Assert.True(accepted);
		Assert.Single(result.Items);
	}

	// ---- TryGiveItem (TraderScript.cs:604-639) ----

	[Fact]
	public void GiveItem_NonPositive_Rejected()
	{
		var state = NewStock();

		Assert.False(TradeStockMachine.TryGiveItem(state, 0).Accepted);
		Assert.False(TradeStockMachine.TryGiveItem(state, -5).Accepted);
	}

	[Fact]
	public void GiveItem_AtCap_Rejected()
	{
		var state = NewStock() with { TotalValueGiven = TradeStockMachine.MaxValueGiven };

		Assert.False(TradeStockMachine.TryGiveItem(state, 10).Accepted);
	}

	[Fact]
	public void GiveItem_CreditsAndCaps()
	{
		var state = NewStock() with { TotalValueGiven = 55f };

		var (accepted, result) = TradeStockMachine.TryGiveItem(state, 20);

		Assert.True(accepted);
		Assert.True(TradeStockMachine.MaxValueGiven == result.ValueGiven, "min(100 + 5, 60) — the lifetime cap covers valueGiven too");
		Assert.True(TradeStockMachine.MaxValueGiven == result.TotalValueGiven, "min(55 + 5, 60)");
	}

	[Fact]
	public void GiveItem_NormalCredit()
	{
		var state = NewStock();

		var (accepted, result) = TradeStockMachine.TryGiveItem(state, 20);

		Assert.True(accepted);
		Assert.True(TradeStockMachine.MaxValueGiven == result.ValueGiven, "min(100 + 20, 60) — valueGiven rides the same lifetime cap");
		Assert.Equal(20f, result.TotalValueGiven);
	}

	// ---- Haggle (TraderScript.cs:220-265) ----

	[Fact]
	public void Haggle_Regular_AddsAmountAndRoll()
	{
		var state = NewStock() with { HaggleAmount = 2f };

		var result = TradeStockMachine.Haggle(state, repRoll: 15f, repRoll2: 0f, biteRoll: 0);

		Assert.True(3f == result.HaggleAmount, "incremented BEFORE the division");
		Assert.True(55f == result.Reputation, "50 + 15 / 3 — the roll divides by the NEW amount");
	}

	[Fact]
	public void Haggle_Cannibal_WithCredit_Bites()
	{
		var state = NewStock() with { Character = 2 };

		var result = TradeStockMachine.Haggle(state, repRoll: 0f, repRoll2: 25f, biteRoll: 8);

		Assert.Equal(75f, result.Reputation);
		Assert.True(TradeStockMachine.MaxValueGiven == result.ValueGiven, "min(100 + 8, 60) — the bite rides the lifetime cap");
		Assert.Equal(8f, result.TotalValueGiven);
	}

	[Fact]
	public void Haggle_Cannibal_AtCap_NoChange()
	{
		var state = NewStock() with { Character = 2, TotalValueGiven = TradeStockMachine.MaxValueGiven };

		var result = TradeStockMachine.Haggle(state, repRoll: 0f, repRoll2: 25f, biteRoll: 8);

		Assert.Equal(50f, result.Reputation);
		Assert.Equal(100f, result.ValueGiven);
	}

	// ---- Threaten (TraderScript.cs:517-545) ----

	[Fact]
	public void Threaten_CutsReputationFirst()
	{
		var state = NewStock();

		var result = TradeStockMachine.Threaten(state, hasGun: false, outcomeRoll: 0.5f, freeRoll: 0);

		Assert.True(Math.Abs(15f - result.Reputation) < 0.001f, "×0.3 BEFORE the outcome branches (float 50 × 0.3 = 15.000001)");
		Assert.True(0f == result.Hostility, "0.5 is in the no-outcome band");
		Assert.Equal(1, result.FreeAmount);
	}

	[Fact]
	public void Threaten_HighRollWithGun_GrantsFreeItems()
	{
		var state = NewStock(); // rep 50 → ×0.3 = 15… needs rep > 30 after the cut
		var strong = NewStock() with { Reputation = 200f };

		// Without the gun, roll 0.8 stays 0.8 → free items (200×0.3=60 > 30).
		var result = TradeStockMachine.Threaten(strong, hasGun: false, outcomeRoll: 0.8f, freeRoll: 3);

		Assert.Equal(1 + 3, result.FreeAmount);

		// With the gun, roll 0.2 lerps to 0.4 — below 0.6, no free items.
		var gunned = TradeStockMachine.Threaten(strong, hasGun: true, outcomeRoll: 0.2f, freeRoll: 3);
		Assert.Equal(1, gunned.FreeAmount);
	}

	[Fact]
	public void Threaten_LowRoll_Hostility()
	{
		var state = NewStock();

		var result = TradeStockMachine.Threaten(state, hasGun: false, outcomeRoll: 0.2f, freeRoll: 0);

		Assert.Equal(100f, result.Hostility);
		Assert.True(Math.Abs(15f - result.Reputation) < 0.001f, "50 × 0.3 = 15.000001 in float");
	}

	[Fact]
	public void Threaten_Cannibal_HalvesTheRoll()
	{
		var state = NewStock() with { Character = 2, Reputation = 200f };

		// 0.8 → ×0.5 = 0.4 — under 0.6, so no free items despite the strong rep.
		var result = TradeStockMachine.Threaten(state, hasGun: false, outcomeRoll: 0.8f, freeRoll: 3);

		Assert.Equal(1, result.FreeAmount);
	}

	[Fact]
	public void Threaten_StrongRollButWeakReputation_NoFreeItems()
	{
		var state = NewStock(); // rep 50 × 0.3 = 15 ≤ 30

		var result = TradeStockMachine.Threaten(state, hasGun: false, outcomeRoll: 0.8f, freeRoll: 3);

		Assert.True(1 == result.FreeAmount, "the roll succeeded but the reputation gate (rep > 30) failed");
	}

	// ---- Hug (TraderScript.cs:448-481) ----

	[Fact]
	public void Hug_BelowMinReputation_PenalizesOnce()
	{
		var state = NewStock(); // rep 50 < minHug 60

		var result = TradeStockMachine.Hug(state, dirty: false);

		Assert.Equal(42f, result.Reputation);
		Assert.True(result.DidHug);

		var second = TradeStockMachine.Hug(result, dirty: false);
		Assert.True(42f == second.Reputation, "the one-shot penalty does not repeat");
	}

	[Fact]
	public void Hug_Dirty_Penalizes()
	{
		var state = NewStock() with { Reputation = 80f };

		var result = TradeStockMachine.Hug(state, dirty: true);

		Assert.Equal(72f, result.Reputation);
		Assert.True(result.DidHug);
	}

	[Fact]
	public void Hug_Accepted_AddsFiveReputation()
	{
		var state = NewStock() with { Reputation = 80f };

		var result = TradeStockMachine.Hug(state, dirty: false);

		Assert.Equal(85f, result.Reputation);
		Assert.True(result.DidHug);
	}

	[Fact]
	public void Hug_Accepted_Cannibal_GetsNoBonus()
	{
		var state = NewStock() with { Reputation = 80f, Character = 2 };

		var result = TradeStockMachine.Hug(state, dirty: false);

		Assert.Equal(80f, result.Reputation);
		Assert.False(result.DidHug, "the cannibal never accepts a hug (no bonus, no flag)");
	}

	[Fact]
	public void Hug_BelowThirty_Hostility()
	{
		var state = NewStock() with { Reputation = 35f }; // 35 - 8 = 27 < 30

		var result = TradeStockMachine.Hug(state, dirty: false);

		Assert.Equal(100f, result.Hostility);
	}

	// ---- MoveTo (TraderScript.cs:89-104) ----

	[Fact]
	public void MoveTo_BelowGate_Penalizes_NoMove()
	{
		var state = NewStock(); // rep 50 < 70

		var result = TradeStockMachine.MoveTo(state);

		Assert.Equal(47f, result.Reputation);
		Assert.False(result.DidMove);
	}

	[Fact]
	public void MoveTo_AboveGate_Moves()
	{
		var state = NewStock() with { Reputation = 80f };

		var result = TradeStockMachine.MoveTo(state);

		Assert.Equal(79f, result.Reputation);
		Assert.True(result.DidMove);
	}
}
