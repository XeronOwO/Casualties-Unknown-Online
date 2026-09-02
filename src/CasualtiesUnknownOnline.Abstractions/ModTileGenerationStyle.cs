using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Preset world-generation shapes for a custom tile's automatic ore-style
/// distribution. The values mirror the CUCoreLib public vocabulary so migrating
/// mods can keep their authored style flags; the Game Adapter interprets them
/// inside CUO's sealed generation stream.
/// </summary>
[Flags]
public enum ModTileGenerationStyle : byte
{
	/// <summary>Disables automatic generation styles.</summary>
	None = 0,

	/// <summary>Standard copper-like vein walker.</summary>
	Vein = 1 << 0,

	/// <summary>Denser, chunkier vein preset.</summary>
	HeavyVeins = 1 << 1,

	/// <summary>Isolated single-tile deposits.</summary>
	Singular = 1 << 2,

	/// <summary>Long stripe-like deposits.</summary>
	Stripe = 1 << 3,

	/// <summary>Biases spawning toward the inner area of a biome layer.</summary>
	Inner = 1 << 4,

	/// <summary>Biases spawning toward the outer edge of a biome layer.</summary>
	Outskirt = 1 << 5
}
