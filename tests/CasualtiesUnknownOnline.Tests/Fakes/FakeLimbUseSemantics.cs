using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The suite's stand-in for the game's own content registries behind
/// <see cref="ILimbUseSemantics"/>. A test host has no game scene, so the vanilla
/// answers the two landed limb-use chains read at runtime (<c>Item.GlobalItems</c>
/// and <c>Liquids.Registry</c>) are listed here instead — the ids the suite
/// exercises, taken from the same decompiled definitions the production reader
/// uses. The production verdict is never this list: it is the game's registry
/// (GameLimbUseFacts), so a stale entry here can only make a suite test wrong,
/// never a live session.
/// </summary>
internal sealed class FakeLimbUseSemantics : ILimbUseSemantics
{
	internal static readonly FakeLimbUseSemantics Instance = new();

	/// <summary>The vanilla liquid containers a limb action draws from (`LiquidItemInfo.usableOnLimb` in Item.cs's SetupItems).</summary>
	private static readonly HashSet<string> LimbUsableContainers = new(StringComparer.Ordinal)
	{
		"morphine", "syringe", "opium", "heroin", "naloxone", "fentanyl", "ceftriaxone",
		"antiserum", "bloodcoagulant", "combatpen", "streptokinase", "bloodbag", "saline",
		"ringersolution", "bloodbaghuman",
		"paincream", "woundglue", "disinfectant", "spraybottle",
	};

	/// <summary>The vanilla liquids whose <c>LiquidType.injectable</c> is set (Liquids.cs).</summary>
	private static readonly HashSet<string> InjectableLiquids = new(StringComparer.Ordinal)
	{
		"saline", "ringersolution", "blood", "redblood", "alienblood", "antiserum", "ceftriaxone",
		"streptokinase", "morphine", "opium", "heroin", "fentanyl", "naloxone", "procoagulant",
		"epinephrine", "oxyline", "amiodarone", "sodiumnitroprusside", "vasopressin", "chloroform",
		"highgradestimulant", "midgradestimulant", "lowgradestimulant", "antivenom", "keratinbooster",
		"biochem",
	};

	/// <summary>The vanilla liquids whose <c>LiquidType.healthUsable</c> is set (Liquids.cs).</summary>
	private static readonly HashSet<string> HealthUsableLiquids = new(StringComparer.Ordinal)
	{
		"alcohol", "bleach", "reliefcream", "woundglue", "disinfectant", "soap",
	};

	/// <summary>
	/// The vanilla items whose own <c>useLimbAction</c> the limb-tool family runs on
	/// the treated player's client. The five the suite exercises plus one item the
	/// deleted catalog never carried (<c>roselight</c>) — the migration's own point:
	/// the game's data decides the family, not a CUO list — plus one id per session
	/// chain that still claims its items (<c>bandage</c>, <c>adhesivebandage</c>,
	/// <c>musharm</c>, <c>aed</c>, <c>machete</c>, <c>wrench</c>, <c>tweezers</c>), so
	/// the admission rule's exclusions are exercised rather than assumed.
	/// </summary>
	private static readonly HashSet<string> LimbActionItems = new(StringComparer.Ordinal)
	{
		"medicalsuture", "boneweldingtool", "clottingmush", "chestdrain", "splint",
		"carcasssplint", "tourniquet", "icepack", "roselight", "bulbskin",
		"glowplantfruit", "antisepticmush", "plasmacutter", "xalorissponge",
		"bandage", "adhesivebandage", "musharm", "aed", "manualdefibrillator",
		"machete", "wrench", "tweezers",
	};

	public bool IsLimbUsableLiquidContainer(string itemId) => LimbUsableContainers.Contains(itemId);

	public bool IsInjectableLiquid(string liquidId) => InjectableLiquids.Contains(liquidId);

	public bool IsHealthUsableLiquid(string liquidId) => HealthUsableLiquids.Contains(liquidId);

	public bool IsLimbActionItem(string itemId) => LimbActionItems.Contains(itemId);
}
