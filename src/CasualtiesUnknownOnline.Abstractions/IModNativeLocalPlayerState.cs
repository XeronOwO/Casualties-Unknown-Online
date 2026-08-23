namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The read-only local-player state projection returned by the native API's
/// <see cref="ModNativeApiOperations.LocalPlayerState"/> operation. This is a
/// framework DTO, not a game-assembly type: a mod reads primitive values only
/// and can never touch the live <c>Body</c> Unity object.
/// </summary>
public interface IModNativeLocalPlayerState
{
	/// <summary>The local body's world X position.</summary>
	float X { get; }

	/// <summary>The local body's world Y position.</summary>
	float Y { get; }

	/// <summary>Brain/primary health (0-100, Body.cs:3950).</summary>
	float BrainHealth { get; }

	/// <summary>Hunger/satiety (negative to 125, Body.cs:3934).</summary>
	float Hunger { get; }

	/// <summary>Thirst (negative to 250, Body.cs:3938).</summary>
	float Thirst { get; }

	/// <summary>Stamina (0-100-ish, Body.cs:3942).</summary>
	float Stamina { get; }

	/// <summary>Energy (0-100-ish, Body.cs:3946).</summary>
	float Energy { get; }

	/// <summary>Body temperature (Body.cs:3965).</summary>
	float Temperature { get; }

	/// <summary>Consciousness value (Body.cs:3954; the game's conscious threshold is &gt;30).</summary>
	float Consciousness { get; }

	/// <summary>Derived from brainHealth by the game (Body.cs:203) — report only.</summary>
	bool Alive { get; }

	/// <summary>Derived by the game (Body.cs:213) — report only.</summary>
	bool Conscious { get; }
}
