namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The read-only vitals projection carried by <see cref="IModPlayerState"/>.
/// Values are the same physiological fields the character-data stream already
/// carries; they are copied at read time and never point at live game objects.
/// </summary>
public interface IModPlayerVitals
{
	/// <summary>The game's primary health value (0-100-ish).</summary>
	float BrainHealth { get; }

	/// <summary>Hunger/satiety.</summary>
	float Hunger { get; }

	/// <summary>Thirst.</summary>
	float Thirst { get; }

	/// <summary>Stamina.</summary>
	float Stamina { get; }

	/// <summary>Energy.</summary>
	float Energy { get; }

	/// <summary>Body temperature.</summary>
	float Temperature { get; }

	/// <summary>Derived alive flag carried on the wire for diagnostics.</summary>
	bool Alive { get; }

	/// <summary>Derived conscious flag carried on the wire for diagnostics.</summary>
	bool Conscious { get; }
}
