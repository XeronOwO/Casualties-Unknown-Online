using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Optional fixed loot sources for a custom item. A mod can choose one or more
/// vanilla loot containers that should always be able to drop the item, instead
/// of (or in addition to) the generic category-based loot pool. The values are
/// deliberately a plain data vocabulary in Abstractions: no game type, no Unity
/// type, no Runtime dependency. The Game Adapter maps these flags to the
/// vanilla crate/corpse/trader source seams.
/// </summary>
[Flags]
public enum ModItemDropSource : ushort
{
	/// <summary>No fixed drop source selected.</summary>
	None = 0,

	/// <summary>Human corpse loot.</summary>
	Corpse = 1 << 0,

	/// <summary>Built-in medical crate (<c>medcrate</c>).</summary>
	MedicalCrate = 1 << 1,

	/// <summary>Built-in food crate (<c>foodbox</c>).</summary>
	FoodCrate = 1 << 2,

	/// <summary>Built-in container crate (<c>containercrate</c>).</summary>
	ContainerCrate = 1 << 3,

	/// <summary>Trader 1 stock.</summary>
	Trader1 = 1 << 4,

	/// <summary>Trader 2 stock.</summary>
	Trader2 = 1 << 5,

	/// <summary>Trader 3 stock.</summary>
	Trader3 = 1 << 6,

	/// <summary>All three trader stock variants.</summary>
	AllTraders = Trader1 | Trader2 | Trader3,

	/// <summary>Built-in drop capsule (<c>dropcapsule</c>).</summary>
	DropCapsule = 1 << 7,

	/// <summary>Built-in capsule container (<c>lifepodchest</c>).</summary>
	CapsuleContainer = 1 << 8,

	/// <summary>Every supported fixed loot source.</summary>
	All = Corpse | MedicalCrate | FoodCrate | ContainerCrate | AllTraders | DropCapsule | CapsuleContainer
}
