namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The four values one shrapnel grip slip puts on the treated limb, exactly as the game's own
/// <c>ShrapnelMinigame.BreakGrasp</c> draws them: the skin it takes away, the bleed and the pain it
/// adds, and the brain health a head limb loses. A head limb whose <c>0.8</c> gate does not pass
/// loses nothing, so <see cref="BrainLoss"/> is zero there.
/// <para>
/// These are the RAW draws, before any clamp: they describe what the roll produced, not what the
/// limb's field ended up holding. That is what makes them comparable across the two machines — the
/// authority derives the same four numbers (see <see cref="ShrapnelGripSlipRoller"/>) and the
/// operator reports the four its own screen's copy of the limb moved by, so a drift between the two
/// draw sequences is a logged fact rather than a silent difference on a screen.
/// </para>
/// </summary>
public readonly record struct ShrapnelGripSlipValues(float Skin, float Bleed, float Pain, float BrainLoss);
