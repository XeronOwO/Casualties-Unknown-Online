using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A recording fake for the Runtime → Game Adapter native-API seam. It lets the
/// mod native-API tests verify the permission/policy layer without loading the
/// game assembly and without touching Unity.
/// </summary>
internal sealed class FakeModNativeApiProvider : IModNativeApiProvider
{
	private readonly List<(string Operation, object?[] Arguments)> _calls = [];

	public bool Available { get; set; } = true;

	public object? Result { get; set; }

	public HashSet<string> RegisteredOperations { get; } =
		[ModNativeApiOperations.LocalPlayerState];

	public IReadOnlyList<(string Operation, object?[] Arguments)> Calls => _calls;

	public bool IsRegistered(string operation) =>
		Available && RegisteredOperations.Contains(operation);

	public bool TryInvoke(string operation, object?[] arguments, out object? result)
	{
		_calls.Add((operation, arguments));
		result = null;

		if (!Available || !RegisteredOperations.Contains(operation))
		{
			return false;
		}

		result = Result;
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
