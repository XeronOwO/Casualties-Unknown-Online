using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod native-API adapter. The common read-only projection
/// (<see cref="ModNativeApiOperations.LocalPlayerState"/>) is exposed both
/// through the generic operation registry and as a typed convenience.
/// </summary>
internal sealed class ModNativeApiAdapter(ModManifest manifest, IModNativeApiProvider nativeApiProvider, ILogger log) : IModNativeApi
{
	public bool CanAccess => ModPermissionGate.HasPermission(manifest, ModPermission.AccessNativeApi);

	public bool CanInvoke(string operation)
	{
		if (!CanAccess || !ModNativeApiPolicy.IsValidOperation(operation))
		{
			return false;
		}

		return nativeApiProvider.IsRegistered(operation);
	}

	public bool TryInvoke(string operation, object?[] arguments, out object? result)
	{
		result = null;

		if (!ModPermissionGate.Try(log, manifest, ModPermission.AccessNativeApi))
		{
			return false;
		}

		if (!ModNativeApiPolicy.IsValidOperation(operation))
		{
			log.LogWarning("[Mods] {ModId} tried to invoke a native operation with an invalid id '{Operation}' — refused.",
				manifest.Id, operation);
			return false;
		}

		if (!ModNativeApiPolicy.IsValidArguments(arguments))
		{
			log.LogWarning("[Mods] {ModId} tried to invoke native operation {Operation} with unsafe/over-cap arguments — refused.",
				manifest.Id, operation);
			return false;
		}

		if (!nativeApiProvider.TryInvoke(operation, arguments, out var nativeResult))
		{
			log.LogWarning("[Mods] {ModId} native operation {Operation} is not available or was refused by the Game Adapter — refused.",
				manifest.Id, operation);
			return false;
		}

		if (!ModNativeApiPolicy.IsSafeResult(nativeResult))
		{
			log.LogWarning("[Mods] {ModId} native operation {Operation} returned an unsafe value type {ValueType} — refused.",
				manifest.Id, operation, nativeResult?.GetType().FullName ?? "null");
			return false;
		}

		result = nativeResult;
		log.LogInformation("[Mods] {ModId} invoked native operation {Operation} ({ArgumentCount} argument(s)).",
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
