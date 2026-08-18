namespace CasualtiesUnknownOnline.Runtime.Protocol;

/// <summary>
/// World entity event kinds — one enum for the whole trap/mechanism event
/// channel. The message carries only the position key + this kind (+ Extra
/// for kind-specific data), never full effect parameters: the explosion
/// constants etc. are compile-time values both sides share, and the receiver
/// derives them from the kind (data-driven replay registry in the adapter).
/// Values start at 1 — protobuf omits zero, and Kind is never "unset".
/// </summary>
public enum EntityEventKind : byte
{
	MinePressed = 31, // landmine pressed: the 0.8 s pre-explosion visual (MineScript.OnCollisionEnter2D: pressed=true + mine sound + pressedSprite) — transient one-way edge; MineExploded remains the durable consumption
	MineExploded = 1, // landmine triggered: pressed → 0.8 s → explosion + entity destroyed (chain explosions included)
	SpikeStabbed = 2, // spikestabber: Stab() ran — one-shot activated state
	BearTrapClamped = 3, // beartrap closed on a limb
	BarbedFenceHit = 4, // barbedwirefence: a hit happened (hitSprite + sound)
	CoilShocked = 5, // coil: an electric shock was delivered
	CactusHit = 6, // cactus: a body bumped it
	JumpPadLaunched = 7, // jumppad: launched something
	StalactiteDropped = 8, // stalactite: dropped off the ceiling (one-shot)
	GeyserActivated = 9, // geyser: rumble → liquid eruption (the liquid type is a generation-time initial condition — GeyserStateSnapshot; Extra unused)
	SoundCannonFired = 10, // soundcannon: fired (one-shot spent)
	TurretFired = 11, // turret: shot a beam (visual only)
	TurretSelfDestructed = 12, // turret: health < 350 countdown finished — explosion + destroyed
	CrystalElectricShocked = 13, // electric crystal: shocked a toucher
	CrystalFragileBroken = 14, // fragile crystal: broken (one-shot)
	CaveTicksSpawned = 15, // cave-tick nest: hatched (one-shot; the spiders ride the EntitySpawned channel + runtime enemy binding)
	BananaPlantSlip = 16, // banana plant: someone slipped on it
	GrabberGrabbed = 17, // grabber plant: grabbed a body
	BearTrapReleased = 18, // beartrap released its caught limb (the clamp is reversible)
	ShuttleDoorOpened = 19, // life-pod shuttle door: a body entered the trigger — the doors open (ShuttleStartOpen; a pure animation entity, not a BuildingEntity)
	LifepodHeatChanged = 20, // life-pod heat button (LifepodButton type 0) — Extra = heatState 0/1/2; repeatable
	LifepodShowerActivated = 21, // life-pod shower button (LifepodButton type 1) — one-shot activated
	BioTerminalUnlocked = 22, // blood terminal unlocked (Backgroundify: terminal + nearby reinforceddoors) — one-shot
	ScrapEaterProgress = 23, // scrap eater fed — Extra = progress % (0-100; 100 = unlocked, Backgroundify + doors) — one-shot at 100
	MedStationHealed = 24, // med station triggered (didHeal + laser anim + heal) — one-shot
	BatteryInserted = 25, // battery charger used — Extra = slot; the insert itself rides the item domain (the battery IS a world item), this syncs the firstTime mp3 consumption — one-shot
	CrystalUnstableExploded = 26, // unstable crystal: touched → 5 s ticking → explosion + destroyed (CrystalUnstable.cs:40-64; the 5 s pre-explosion ticking is a recorded replay gap, same as the mine's 0.8 s press) — one-shot
	CrystalMetamorphicTriggered = 27, // metamorphic crystal: touched → FlashBrief + health = 0 + drops + laugh (CrystalMetamorphic.cs:16-35; the death rides BuildingEntityDamaged, the drops ride the item domain — this syncs the flash/laugh consumption) — one-shot
	CrystalShySwapped = 28, // shy crystal: touched → swaps positions with the first other crystal in range (CrystalShy.cs:8-33; the swap is a real world-state change) — one-shot
	CrystalEMPActivated = 29, // EMP crystal: touched → battery drain + white flash + shake (CrystalEMP.cs:14-35; the battery effects ride the item domain, the darkening runs on the crystal's own Update) — one-shot
	CrystalMimicTriggered = 30, // mimic crystal: touched/attacked → activated latch + observerlaugh + crystalenemy spawns (the enemies ride EntitySpawned + EnemyRuntimeSpawn; this syncs the one-shot latch)
}
