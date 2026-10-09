using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A fake for the Runtime → Game Adapter native-API seam. It lets the mod
/// native-API tests verify the permission gate and the typed projection without
/// loading the game assembly and without touching Unity. The seam has one entry
/// per operation, so the fake records how often that entry was reached instead
/// of a generic call log.
/// </summary>
internal sealed class FakeModNativeApiProvider : IModNativeApiProvider
{
	public int LocalPlayerStateCalls { get; private set; }

	public int RegistrationProbes { get; private set; }

	public bool Available { get; set; } = true;

	public IModNativeLocalPlayerState? Result { get; set; }

	public HashSet<string> RegisteredOperations { get; } =
		[ModNativeApiOperations.LocalPlayerState];

	public bool IsRegistered(string operation)
	{
		RegistrationProbes++;
		return Available && RegisteredOperations.Contains(operation);
	}

	public bool TryGetLocalPlayerState(out IModNativeLocalPlayerState state)
	{
		LocalPlayerStateCalls++;

		if (!Available || Result is null)
		{
			state = null!;
			return false;
		}

		state = Result;
		return true;
	}
}

/// <summary>A simple immutable DTO used by the native-API tests.</summary>
internal sealed class FakeNativeLocalPlayerState(
	float x,
	float y,
	float brainHealth,
	float hunger,
	float thirst,
	float stamina,
	float energy,
	float temperature,
	float consciousness,
	bool alive,
	bool conscious) : IModNativeLocalPlayerState
{
	public float X { get; } = x;

	public float Y { get; } = y;

	public float BrainHealth { get; } = brainHealth;

	public float Hunger { get; } = hunger;

	public float Thirst { get; } = thirst;

	public float Stamina { get; } = stamina;

	public float Energy { get; } = energy;

	public float Temperature { get; } = temperature;

	public float Consciousness { get; } = consciousness;

	public bool Alive { get; } = alive;

	public bool Conscious { get; } = conscious;
}
