using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The mod native-API half of <see cref="GameAdapter"/> (Phase 4 Mod API
/// remainder). This is the Game Adapter side of the Runtime boundary: it owns
/// the only registered read-only operation in this slice,
/// <see cref="ModNativeApiOperations.LocalPlayerState"/>, implemented directly
/// from the local <c>Body</c> (the Game Adapter is the only layer allowed to
/// know that private game type). No Unity object crosses the seam — the result
/// is the framework DTO in <see cref="Abstractions"/>.
/// </summary>
public sealed partial class GameAdapter : IModNativeApiProvider
{
	bool IModNativeApiProvider.IsRegistered(string operation) =>
		operation == ModNativeApiOperations.LocalPlayerState;

	bool IModNativeApiProvider.TryInvoke(string operation, object?[] arguments, out object? result)
	{
		result = null;

		if (operation != ModNativeApiOperations.LocalPlayerState || arguments.Length != 0)
		{
			return false;
		}

		var body = _run.LocalBody;
		if (body == null) // Unity object — == (scene-reload check)
		{
			return false;
		}

		var position = body.transform.position;
		result = new NativeLocalPlayerState(
			position.x,
			position.y,
			body.brainHealth,
			body.hunger,
			body.thirst,
			body.stamina,
			body.energy,
			body.temperature,
			body.consciousness,
			body.alive,
			body.conscious);
		return true;
	}

	private sealed class NativeLocalPlayerState(
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
}
