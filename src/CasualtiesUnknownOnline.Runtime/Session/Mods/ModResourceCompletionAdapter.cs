using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod <see cref="IModResourceCompletion"/> adapter. It scopes the stage
/// table to one mod id, validates every registration (null stage, blank or
/// over-long id, duplicate, per-mod cap) and keeps
/// <see cref="ModResourceCompletionStore"/> — the table the catalog reads — in
/// step with it. This is a separate top-level type from <see cref="ModContext"/>
/// so the context stays under the architecture line cap; it lives in the mods
/// domain because the mod identity is what the scoping rule is made of.
/// </summary>
internal sealed class ModResourceCompletionAdapter(
	ModResourceCompletionStore store,
	ModManifest manifest,
	ILogger log) : IModResourceCompletion
{
	/// <summary>The framework's cap on the stages one mod may contribute.</summary>
	internal const int MaxMatchStagesPerMod = 8;

	/// <summary>The longest accepted stage id (a mod-chosen handle, never shown to the player).</summary>
	internal const int MaxMatchStageIdLength = 128;

	private readonly Dictionary<string, IResourceLocationMatchStage> _stages = [with(StringComparer.Ordinal)];

	public bool TryRegisterMatchStage(string id, IResourceLocationMatchStage stage)
	{
		if (stage is null)
		{
			log.LogWarning("[ContentId] {ModId} tried to register a null resource-completion stage — refused.",
				manifest.Id);
			return false;
		}

		if (!IsValidStageId(id))
		{
			log.LogWarning("[ContentId] {ModId} tried to register a resource-completion stage with an invalid id '{Id}' — refused.",
				manifest.Id, id);
			return false;
		}

		if (_stages.ContainsKey(id))
		{
			log.LogWarning("[ContentId] {ModId} already registered the resource-completion stage {StageId} — the duplicate is refused.",
				manifest.Id, id);
			return false;
		}

		if (_stages.Count >= MaxMatchStagesPerMod)
		{
			log.LogWarning("[ContentId] {ModId} reached the {Cap}-stage completion cap — {StageId} refused.",
				manifest.Id, MaxMatchStagesPerMod, id);
			return false;
		}

		_stages.Add(id, stage);
		store.Add(stage);
		log.LogInformation("[ContentId] {ModId} registered the resource-completion stage {StageId}.",
			manifest.Id, id);
		return true;
	}

	public bool TryUnregisterMatchStage(string id)
	{
		if (id is null || !_stages.TryGetValue(id, out var stage))
		{
			return false;
		}

		_stages.Remove(id);
		store.Remove(stage);
		log.LogInformation("[ContentId] {ModId} unregistered the resource-completion stage {StageId}.",
			manifest.Id, id);
		return true;
	}

	public bool IsMatchStageRegistered(string id) => id is not null && _stages.ContainsKey(id);

	public IReadOnlyCollection<string> MatchStageIds => [.. _stages.Keys];

	public int MatchStageCount => _stages.Count;

	/// <summary>The stage-id rails: a non-blank handle within the length cap.</summary>
	internal static bool IsValidStageId(string? id) =>
		!string.IsNullOrWhiteSpace(id) && id!.Length <= MaxMatchStageIdLength;
}
