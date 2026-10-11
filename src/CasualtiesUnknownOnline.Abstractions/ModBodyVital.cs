namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The body vitals <see cref="IModEffectBody"/> reads and writes, one member per
/// write the game's own shipped content performs: the liquid census in
/// <c>docs/backlog/done/mod-authored-effects.md</c>, plus the item census in
/// <c>docs/backlog/done/mod-item-authored-actions.md</c> — whose one addition to
/// this list is <see cref="Hemothorax"/>, everything else an item writes on a body
/// already being here for the liquids. The names are CUO's; each member's
/// documentation names the game's own field it stands for, because the level is
/// <see cref="ApiStabilityLevel.Advanced"/> and the game is what the shape follows.
///
/// <para>
/// Values are the game's own units and ranges — the framework never clamps or
/// scales them, exactly as it never does for the game's own effects. Several of
/// these fields have natural bounds the game enforces at its own read sites
/// rather than on write, so an effect that ignores them behaves like a vanilla
/// effect that ignores them.
/// </para>
/// </summary>
[ApiStability(ApiStabilityLevel.Advanced)]
public enum ModBodyVital
{
	/// <summary>Thirst (<c>Body.thirst</c>); <see cref="IModEffectBody.Drink"/> is the action form.</summary>
	Thirst,

	/// <summary>Hunger/satiety (<c>Body.hunger</c>).</summary>
	Hunger,

	/// <summary>Body temperature (<c>Body.temperature</c>).</summary>
	Temperature,

	/// <summary>Happiness/mood (<c>Body.happiness</c>).</summary>
	Happiness,

	/// <summary>Sickness (<c>Body.sicknessAmount</c>).</summary>
	Sickness,

	/// <summary>The weight offset a drink adds (<c>Body.weightOffset</c>).</summary>
	Weight,

	/// <summary>Stamina (<c>Body.stamina</c>).</summary>
	Stamina,

	/// <summary>Energy (<c>Body.energy</c>).</summary>
	Energy,

	/// <summary>Consciousness (<c>Body.consciousness</c>; the game's conscious threshold is above 30).</summary>
	Consciousness,

	/// <summary>Adrenaline (<c>Body.adrenaline</c>).</summary>
	Adrenaline,

	/// <summary>Shock (<c>Body.shock</c>).</summary>
	Shock,

	/// <summary>Caffeine level (<c>Body.caffeinated</c>).</summary>
	Caffeine,

	/// <summary>Dirtiness (<c>Body.dirtyness</c>).</summary>
	Dirtiness,

	/// <summary>Brain/primary health (<c>Body.brainHealth</c>).</summary>
	BrainHealth,

	/// <summary>Blood oxygen (<c>Body.bloodOxygen</c>).</summary>
	BloodOxygen,

	/// <summary>Blood viscosity (<c>Body.bloodViscosity</c>).</summary>
	BloodViscosity,

	/// <summary>Blood volume (<c>Body.bloodVolume</c>).</summary>
	BloodVolume,

	/// <summary>Internal bleeding rate (<c>Body.internalBleeding</c>).</summary>
	InternalBleeding,

	/// <summary>Stroke amount (<c>Body.strokeAmount</c>).</summary>
	Stroke,

	/// <summary>Septic shock (<c>Body.septicShock</c>).</summary>
	SepticShock,

	/// <summary>Radiation sickness (<c>Body.radiationSickness</c>).</summary>
	RadiationSickness,

	/// <summary>Venom total (<c>Body.venomTotal</c>).</summary>
	Venom,

	/// <summary>Remaining antibiotic immunity (<c>Body.antibioticImmunityTime</c>).</summary>
	AntibioticImmunity,

	/// <summary>Claw regrowth time (<c>Body.clawRegrowTime</c>).</summary>
	ClawRegrowth,

	/// <summary>Brain-grow sickness (<c>Body.brainGrowSickness</c>).</summary>
	BrainGrowSickness,

	/// <summary>Heart rate (<c>Body.heartRate</c>).</summary>
	HeartRate,

	/// <summary>Fibrillation progress (<c>Body.fibrillationProgress</c>).</summary>
	Fibrillation,

	/// <summary>Blood pressure change from medicine (<c>Body.bloodPressureChangeFromMedicine</c>).</summary>
	MedicineBloodPressure,

	/// <summary>Respiratory rate (<c>Body.respiratoryRate</c>).</summary>
	RespiratoryRate,

	/// <summary>Stimulant multiplier (<c>Body.stimulantMultiplier</c>).</summary>
	StimulantMultiplier,

	/// <summary>Shake intensity (<c>Body.miscShakeIntensity</c>).</summary>
	ShakeIntensity,

	/// <summary>
	/// Overdose index (<c>Body.overdoseIndex</c>). INTEGER-valued in the game: a
	/// write is truncated toward zero, and a read returns the integer as a float.
	/// </summary>
	OverdoseIndex,

	/// <summary>
	/// Blood in the chest cavity (<c>Body.hemothorax</c>) — the item half's one
	/// addition to the liquid census, written by a chest-drain limb action
	/// (<c>limb.body.hemothorax -= 35f</c>). It lowers blood oxygen and blood
	/// pressure at the game's own read sites.
	/// </summary>
	Hemothorax,
}
