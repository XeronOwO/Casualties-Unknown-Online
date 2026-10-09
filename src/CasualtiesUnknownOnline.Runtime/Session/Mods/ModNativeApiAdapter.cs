using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod native-API adapter: the permission gate plus the operation's own
/// typed projection. There is no generic invoke path — a mod reaches a native
/// operation through a method whose signature declares that operation's result,
/// so the permission check and the availability check are the only things that
/// decide the outcome.
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

	public bool TryGetLocalPlayerState(out IModNativeLocalPlayerState state)
	{
		state = null!;

		if (!ModPermissionGate.Try(log, manifest, ModPermission.AccessNativeApi))
		{
			return false;
		}

		if (!nativeApiProvider.TryGetLocalPlayerState(out var result))
		{
			log.LogWarning("[Mods] {ModId} native operation {Operation} is not available (no local body, or the Game Adapter does not provide it) — refused.",
				manifest.Id, ModNativeApiOperations.LocalPlayerState);
			return false;
		}

		state = result;
		log.LogInformation("[Mods] {ModId} invoked native operation {Operation}.",
			manifest.Id, ModNativeApiOperations.LocalPlayerState);
		return true;
	}
}
