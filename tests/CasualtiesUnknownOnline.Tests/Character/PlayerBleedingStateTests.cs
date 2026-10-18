using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Character;

/// <summary>
/// The host's continuous bleeding model (the 2026-10-18 ruling): the values the host owns, the
/// inputs it only reads, and the game rules it re-runs. Each row pins one rule against the
/// native behaviour quoted in the model's own documentation, so a later edit that "simplifies"
/// a drain away fails here instead of in a two-client session.
/// </summary>
public class PlayerBleedingStateTests
{
	[Fact]
	public void Seed_TakesTheOwnedValuesAndTheInputs()
	{
		var state = PlayerBleedingState.Seed(Snapshot(80f, Limb(0, 12f, skinHealth: 90f)), healingRate: 1f, bleedRate: 1f);

		Assert.Equal(80f, state.BloodVolume, 3);
		Assert.True(state.TryGetBleed(0, out var bleed));
		Assert.Equal(12f, bleed, 3);
		Assert.Equal(1, state.LimbCount);
	}

	[Fact]
	public void Advance_DecaysTheBleedAndDrainsTheBloodVolume()
	{
		var state = PlayerBleedingState.Seed(Snapshot(100f, Limb(0, 10f)), healingRate: 1f, bleedRate: 1f);

		state.Advance(1f);

		Assert.True(state.TryGetBleed(0, out var bleed));
		Assert.True(bleed < 10f, $"the wound must clot, got {bleed}.");
		Assert.True(state.BloodVolume < 100f, $"a bleeding wound must drain the blood volume, got {state.BloodVolume}.");
		Assert.True(state.TotalBleedSpeed > 0f, "an unblocked wound reads as a bleeding speed.");
	}

	[Fact]
	public void TotalBleedSpeed_UsesTheNativeBleedSpeedMultiplier()
	{
		// Body.cs:2807 multiplies each limb's bleed by its bleedSpeedMult, and Limb.bleedSpeedMult
		// (Limb.cs:22) is Body.bleedingSpeedMultiplier — at viscosity 0 that is
		// (0.01 + Clamp01(Remap(0, -100, 0, 0.01, 0))) * bleedrate = 0.01 (Body.cs:3523). A
		// 10-unit wound therefore reads 0.1 l/m, not 10; the panel's thresholds live in that range.
		var state = PlayerBleedingState.Seed(Snapshot(100f, Limb(0, 10f)), healingRate: 1f, bleedRate: 1f);

		Assert.Equal(0.1f, state.TotalBleedSpeed, 4);
	}

	[Fact]
	public void TotalBleedSpeed_AddsTheInternalDrainAndSubtractsTheRegeneration()
	{
		// Body.cs:2817-2818: the internal bleeding joins at the same 0.0057 rate and the body's
		// regeneration comes off, then the floor at zero (Body.cs:2819-2822).
		var state = PlayerBleedingState.Seed(new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { BloodVolume = 100f, Hunger = 100f, InternalBleeding = 25f },
			Limbs = [],
		}, healingRate: 1f, bleedRate: 1f);

		// 25 * 0.0057 = 0.1425, minus the fed body's 0.035 = 0.1075.
		Assert.Equal(0.1075f, state.TotalBleedSpeed, 4);
	}

	[Fact]
	public void Advance_AShrapnelWoundDoesNotClotButStillBleeds()
	{
		// Limb.cs:524 — the shrapnel factor zeroes the decay; Limb.cs:500 still drains.
		var state = PlayerBleedingState.Seed(Snapshot(100f, Limb(0, 10f, shrapnel: 3)), healingRate: 1f, bleedRate: 1f);

		state.Advance(1f);

		Assert.True(state.TryGetBleed(0, out var bleed));
		Assert.Equal(10f, bleed, 3);
		Assert.Equal(99.9f, state.BloodVolume, 3);
	}

	[Fact]
	public void Advance_ABandageDecaysTheBleedAtOneAndAQuarterPerSecond()
	{
		// Limb.cs:524-525 — the bandage's own 1.25/s on top of the clotting.
		var state = PlayerBleedingState.Seed(Snapshot(100f, Limb(0, 10f, bandageSlow: 10f)), healingRate: 1f, bleedRate: 1f);

		state.Advance(1f);

		Assert.True(state.TryGetBleed(0, out var bleed));
		Assert.Equal(8.725f, bleed, 3);
	}

	[Fact]
	public void Advance_TheVenomDrainAppliesOnlyWhilePositive()
	{
		// Body.cs:2861 — only while venomCurrent > 0; a negative report must never add blood.
		var positive = PlayerBleedingState.Seed(new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { BloodVolume = 100f, VenomCurrent = 500f },
			Limbs = [],
		}, healingRate: 1f, bleedRate: 1f);
		positive.Advance(1f);
		Assert.Equal(99f, positive.BloodVolume, 3);

		var negative = PlayerBleedingState.Seed(new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { BloodVolume = 100f, VenomCurrent = -500f },
			Limbs = [],
		}, healingRate: 1f, bleedRate: 1f);
		negative.Advance(1f);
		Assert.Equal(100f, negative.BloodVolume, 3);
	}

	[Fact]
	public void Advance_ClampsTheBloodVolumeToTheNativeRange()
	{
		// Body.cs:688-696 — the [-100, 200] clamp lives in the circulation step.
		var high = PlayerBleedingState.Seed(new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { BloodVolume = 250f },
			Limbs = [],
		}, healingRate: 1f, bleedRate: 1f);
		high.Advance(1f);
		Assert.Equal(200f, high.BloodVolume, 3);

		var low = PlayerBleedingState.Seed(new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { BloodVolume = -150f },
			Limbs = [],
		}, healingRate: 1f, bleedRate: 1f);
		low.Advance(1f);
		Assert.Equal(-100f, low.BloodVolume, 3);
	}

	[Fact]
	public void Advance_TheInternalBleedingDrainReadsAtMostTwentyFive()
	{
		// Body.cs:2799 / 3674 — the drain reads IntBleedingClamped, capped at 25.
		var state = PlayerBleedingState.Seed(new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { BloodVolume = 100f, InternalBleeding = 100f },
			Limbs = [],
		}, healingRate: 1f, bleedRate: 1f);

		state.Advance(1f);

		Assert.Equal(100f - (25f * 0.0057f), state.BloodVolume, 3);
	}

	[Fact]
	public void Advance_UsesTheRunsHealingAndBleedRates()
	{
		// The world settings are live (a widened run clots and bleeds at its own speeds).
		var state = PlayerBleedingState.Seed(Snapshot(100f, Limb(0, 10f)), healingRate: 2f, bleedRate: 2f);

		state.Advance(1f);

		Assert.True(state.TryGetBleed(0, out var bleed));
		Assert.Equal(9.95f, bleed, 3); // 10 - 2 * 0.025
		Assert.Equal(99.8f, state.BloodVolume, 3); // 100 - 10 * (0.01 * 2)
	}

	[Fact]
	public void NonFiniteInputs_AreIgnoredSoAdvanceCannotThrow()
	{
		// A corrupt report must not poison the state; Math.Sign(NaN) would throw in MoveTowards.
		var state = PlayerBleedingState.Seed(new CharacterDataMsg
		{
			Health = new CharacterHealthMsg
			{
				BloodVolume = float.NaN,
				BloodViscosity = float.NaN,
				VenomCurrent = float.NaN,
				Hunger = float.NaN,
				InternalBleeding = float.NaN,
			},
			Limbs = [new CharacterLimbMsg { Index = 0, BleedAmount = float.NaN, SkinHealth = float.NaN }],
		}, healingRate: 1f, bleedRate: 1f);

		state.Advance(0.1f);

		Assert.True(!float.IsNaN(state.BloodVolume) && !float.IsInfinity(state.BloodVolume));
		Assert.True(!float.IsNaN(state.TotalBleedSpeed) && !float.IsInfinity(state.TotalBleedSpeed));
		Assert.True(state.TryGetBleed(0, out var bleed));
		Assert.True(!float.IsNaN(bleed) && !float.IsInfinity(bleed));
	}

	[Fact]
	public void Advance_ATourniquetStopsTheDrainButNotTheClotting()
	{
		var state = PlayerBleedingState.Seed(Snapshot(100f, Limb(0, 10f, blocked: true)), healingRate: 1f, bleedRate: 1f);

		state.Advance(1f);

		Assert.Equal(100f, state.BloodVolume, 3);
		Assert.True(state.TryGetBleed(0, out var bleed));
		Assert.True(bleed < 10f, "a blocked limb still clots.");
		Assert.Equal(0f, state.TotalBleedSpeed, 3);
	}

	[Fact]
	public void Advance_ADismemberedLimbNeitherBleedsNorClots()
	{
		var state = PlayerBleedingState.Seed(Snapshot(100f, Limb(0, 10f)), healingRate: 1f, bleedRate: 1f);

		// The input says the limb is gone: it stops draining and it is not written back.
		state.ApplyInputs(Snapshot(100f, Limb(0, 10f, dismembered: true)));
		state.Advance(1f);

		Assert.False(state.TryGetBleed(0, out _));
		Assert.Equal(100f, state.BloodVolume, 3);
	}

	[Fact]
	public void Advance_ClampsTheBleedToWhatTheSkinCanStillHold()
	{
		var state = PlayerBleedingState.Seed(Snapshot(100f, Limb(0, 10f, skinHealth: 95f)), healingRate: 1f, bleedRate: 1f);

		state.Advance(0.001f);

		Assert.True(state.TryGetBleed(0, out var bleed));
		Assert.True(bleed <= 5f + 0.001f, $"the native clamp is 100 - skinHealth, got {bleed}.");
	}

	[Fact]
	public void Advance_RegeneratesTowardOneHundredWhenFed()
	{
		var state = PlayerBleedingState.Seed(new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { BloodVolume = 50f, Hunger = 100f },
			Limbs = [],
		}, healingRate: 1f, bleedRate: 1f);

		state.Advance(1f);

		Assert.True(state.BloodVolume > 50f, $"the native regeneration must lift a low volume, got {state.BloodVolume}.");
	}

	[Fact]
	public void Advance_ZeroDeltaChangesNothing()
	{
		var state = PlayerBleedingState.Seed(Snapshot(80f, Limb(0, 10f)), healingRate: 1f, bleedRate: 1f);

		state.Advance(0f);

		Assert.Equal(80f, state.BloodVolume, 3);
		Assert.True(state.TryGetBleed(0, out var bleed));
		Assert.Equal(10f, bleed, 3);
	}

	[Fact]
	public void ApplyAuthority_AdoptsTheEventValuesAndInputsDoNot()
	{
		var state = PlayerBleedingState.Seed(Snapshot(100f, Limb(0, 0f)), healingRate: 1f, bleedRate: 1f);

		// A report that only carries inputs must not move the owned values...
		state.ApplyInputs(Snapshot(20f, Limb(0, 40f)));
		Assert.Equal(100f, state.BloodVolume, 3);

		// ...while an authoritative event write (the host's own) is adopted.
		state.ApplyAuthority(Snapshot(20f, Limb(0, 40f)));
		Assert.Equal(20f, state.BloodVolume, 3);
		Assert.True(state.TryGetBleed(0, out var bleed));
		Assert.Equal(40f, bleed, 3);
	}

	[Fact]
	public void PlayerVitalsMsg_RoundTripsThroughTheWire()
	{
		var msg = new PlayerVitalsMsg
		{
			SteamId = 76561198000000042UL,
			BloodVolume = 62.5f,
			TotalBleedSpeed = 0.42f,
			Limbs = [new PlayerLimbBleedMsg { Index = 2, BleedAmount = 7.25f }],
		};

		var decoded = NetPacket.DecodePayload<PlayerVitalsMsg>(NetPacket.Encode(NetMsg.PlayerVitals, msg));

		Assert.Equal(msg.SteamId, decoded.SteamId);
		Assert.Equal(62.5f, decoded.BloodVolume, 3);
		Assert.Equal(0.42f, decoded.TotalBleedSpeed, 3);
		var limb = Assert.Single(decoded.Limbs);
		Assert.Equal(2, limb.Index);
		Assert.Equal(7.25f, limb.BleedAmount, 3);
	}

	private static CharacterDataMsg Snapshot(float bloodVolume, params CharacterLimbMsg[] limbs) => new()
	{
		Health = new CharacterHealthMsg { BloodVolume = bloodVolume },
		Limbs = [.. limbs],
	};

	/// <summary>
	/// A limb row whose skin is already broken (the native clamp is <c>100 - skinHealth</c>, so a
	/// limb at full skin can hold no bleed at all — the game's own rule, not this model's).
	/// </summary>
	private static CharacterLimbMsg Limb(int index, float bleed, float skinHealth = 50f, bool blocked = false, bool dismembered = false,
		int shrapnel = 0, float bandageSlow = 0f) => new()
		{
			Index = index,
			BleedAmount = bleed,
			SkinHealth = skinHealth,
			BlockedBleeding = blocked,
			Dismembered = dismembered,
			Shrapnel = shrapnel,
			BandageSlowAmount = bandageSlow,
		};
}
