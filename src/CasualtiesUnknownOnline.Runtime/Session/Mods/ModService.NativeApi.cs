using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// Mod native-API half of <see cref="ModService"/> (Phase 4 Mod API remainder).
/// Each mod gets a permission-gated adapter: the framework validates the
/// operation id, argument shape and result value surface, then forwards the
/// call to the Game Adapter's <see cref="IModNativeApiProvider"/> seam. The
/// adapter is deliberately a registry, not arbitrary reflection — only the
/// operations the Game Adapter registers are reachable, and no Unity or
/// game-assembly type ever crosses the boundary.
/// </summary>
public sealed partial class ModService
{
	/// <summary>
	/// The per-mod native-API adapter. The common read-only projection
	/// (<see cref="ModNativeApiOperations.LocalPlayerState"/>) is exposed both
	/// through the generic operation registry and as a typed convenience.
	/// </summary>
	private sealed class ModNativeApiAdapter(ModService owner, ModManifest manifest) : IModNativeApi
	{
		public bool CanAccess => HasPermission(manifest, ModPermission.AccessNativeApi);

		public bool CanInvoke(string operation)
		{
			if (!CanAccess || !ModNativeApiPolicy.IsValidOperation(operation))
			{
				return false;
			}

			return owner._nativeApiProvider.IsRegistered(operation);
		}

		public bool TryInvoke(string operation, object?[] arguments, out object? result)
		{
			result = null;

			if (!CanAccess)
			{
				owner.LogMissingPermission(manifest.Id, "AccessNativeApi");
				return false;
			}

			if (!ModNativeApiPolicy.IsValidOperation(operation))
			{
				owner._log.LogWarning("[Mods] {ModId} tried to invoke a native operation with an invalid id '{Operation}' — refused.",
					manifest.Id, operation);
				return false;
			}

			if (!ModNativeApiPolicy.IsValidArguments(arguments))
			{
				owner._log.LogWarning("[Mods] {ModId} tried to invoke native operation {Operation} with unsafe/over-cap arguments — refused.",
					manifest.Id, operation);
				return false;
			}

			if (!owner._nativeApiProvider.TryInvoke(operation, arguments, out var nativeResult))
			{
				owner._log.LogWarning("[Mods] {ModId} native operation {Operation} is not available or was refused by the Game Adapter — refused.",
					manifest.Id, operation);
				return false;
			}

			if (!ModNativeApiPolicy.IsSafeResult(nativeResult))
			{
				owner._log.LogWarning("[Mods] {ModId} native operation {Operation} returned an unsafe value type {ValueType} — refused.",
					manifest.Id, operation, nativeResult?.GetType().FullName ?? "null");
				return false;
			}

			result = nativeResult;
			owner._log.LogInformation("[Mods] {ModId} invoked native operation {Operation} ({ArgumentCount} argument(s)).",
				manifest.Id, operation, arguments.Length);
			return true;
		}

		public bool TryGetLocalPlayerState(out IModNativeLocalPlayerState state)
		{
			state = null!;

			if (TryInvoke(ModNativeApiOperations.LocalPlayerState, [], out var result)
				&& result is IModNativeLocalPlayerState localState)
			{
				state = localState;
				return true;
			}

			return false;
		}
	}
}
