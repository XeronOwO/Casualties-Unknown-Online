using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod <see cref="IModMoodleRuntime"/> adapter. It scopes the resolver
/// table to one mod id and applies the same light validation used by the
/// runtime status surface; the primitive table lives in
/// <see cref="ModStatusStore"/>. This is a separate top-level type so the store
/// stays under the architecture gate while keeping the adapter in the
/// mod-status domain.
/// </summary>
internal sealed class ModStatusMoodleRuntimeAdapter(
	ModStatusStore store,
	ModManifest manifest,
	ILogger log) : IModMoodleRuntime
{
	public bool TryRegisterResolver(string statusId, Func<ModStatusMoodleRequest, string?> resolver)
	{
		if (resolver is null)
		{
			log.LogWarning("[Mods] {ModId} tried to register a null moodle resolver for {StatusId} — refused.",
				manifest.Id, statusId);
			return false;
		}

		if (!ModStatusPolicy.IsValidStatusId(statusId))
		{
			log.LogWarning("[Mods] {ModId} tried to register a moodle resolver with an invalid status id {StatusId} — refused.",
				manifest.Id, statusId);
			return false;
		}

		return store.TryRegisterMoodleResolver(manifest.Id, statusId, resolver);
	}

	public bool TryUnregisterResolver(string statusId)
	{
		if (!ModStatusPolicy.IsValidStatusId(statusId))
		{
			return false;
		}

		return store.TryUnregisterMoodleResolver(manifest.Id, statusId);
	}

	public bool HasResolver(string statusId) =>
		ModStatusPolicy.IsValidStatusId(statusId) && store.HasMoodleResolver(manifest.Id, statusId);

	public IReadOnlyCollection<string> ResolverStatusIds => store.GetMoodleResolverStatusIds(manifest.Id);

	public int ResolverCount => store.GetMoodleResolverCount(manifest.Id);
}
