using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Binds <see cref="ModMoodleDefinition"/> payloads from shared-content mods
/// into a small, validated static moodle/presentation descriptor registry.
/// This provider intentionally does not feed the vanilla moodle manager: custom
/// moodle display is a GameAdapter/local-UI concern and needs a real UI seam.
/// The typed content registry is the stable migration base for that later work.
/// </summary>
public sealed class GameAdapterMoodleContentProvider(
	ILogger<GameAdapterMoodleContentProvider> log) : IContentBindingProvider, ICuoService
{
	private readonly ILogger<GameAdapterMoodleContentProvider> _log = log;
	private readonly Dictionary<string, ModMoodleDefinition> _definitions = [];

	/// <inheritdoc />
	public string Kind => ModContentKind.Moodle;

	/// <inheritdoc />
	public bool TryBind(ModContentRegistration registration)
	{
		if (!string.Equals(registration.Definition.Kind, Kind, StringComparison.Ordinal))
		{
			return false;
		}

		var definition = ModMoodleDefinition.FromPayload(registration.Definition.Data);
		if (definition is null)
		{
			_log.LogWarning(
				"[MoodleContent] {ModId}/{Id} payload is not a valid ModMoodleDefinition — refused.",
				registration.ModId, registration.Definition.Id);
			return false;
		}

		var id = registration.Definition.Id;
		if (string.IsNullOrWhiteSpace(id))
		{
			_log.LogWarning("[MoodleContent] {ModId} registered a moodle with an empty id — refused.", registration.ModId);
			return false;
		}

		if (string.IsNullOrWhiteSpace(definition.IconId))
		{
			_log.LogWarning(
				"[MoodleContent] {ModId}/{Id} has no IconId — refused (a stable icon key is required for a moodle definition).",
				registration.ModId, id);
			return false;
		}

		if (definition.IconId.Length > 256)
		{
			_log.LogWarning(
				"[MoodleContent] {ModId}/{Id} IconId is longer than the 256-character seam limit — refused.",
				registration.ModId, id);
			return false;
		}

		if (definition.Intensity < 0)
		{
			_log.LogWarning(
				"[MoodleContent] {ModId}/{Id} has a negative intensity {Intensity} — refused.",
				registration.ModId, id, definition.Intensity);
			return false;
		}

		if (definition.HoldSeconds < 0f)
		{
			_log.LogWarning(
				"[MoodleContent] {ModId}/{Id} has a negative HoldSeconds — refused.",
				registration.ModId, id);
			return false;
		}

		if (!ValidateAnimation(registration.ModId, id, definition.IconAnimation))
		{
			return false;
		}

		if (_definitions.ContainsKey(id))
		{
			_log.LogWarning(
				"[MoodleContent] {ModId}/{Id} is already registered by another moodle-content provider/definition — refused.",
				registration.ModId, id);
			return false;
		}

		_definitions.Add(id, definition);
		_log.LogInformation(
			"[MoodleContent] accepted {ModId}/{Id} (intensity {Intensity}, icon {Icon}; schema {SchemaVersion}).",
			registration.ModId, id, definition.Intensity, definition.IconId, registration.Definition.SchemaVersion);
		return true;
	}

	private bool ValidateAnimation(string modId, string id, ModMoodleAnimation? animation)
	{
		if (animation is null)
		{
			return true;
		}

		if (animation.FramePaths is not { Count: > 0 }
			|| float.IsNaN(animation.FramesPerSecond)
			|| float.IsInfinity(animation.FramesPerSecond)
			|| animation.FramesPerSecond <= 0f)
		{
			_log.LogWarning(
				"[MoodleContent] {ModId}/{Id} has an invalid icon animation — refused.",
				modId, id);
			return false;
		}

		var hasFrame = false;
		foreach (var path in animation.FramePaths)
		{
			if (!string.IsNullOrWhiteSpace(path))
			{
				hasFrame = true;
				break;
			}
		}

		if (!hasFrame)
		{
			_log.LogWarning(
				"[MoodleContent] {ModId}/{Id} has an empty icon animation — refused.",
				modId, id);
			return false;
		}

		return true;
	}

	public void Initialize()
	{
	}

	public void Start()
	{
	}

	public void Update()
	{
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}

	/// <summary>Resolve a bound static moodle descriptor.</summary>
	internal bool TryGetDefinition(string id, out ModMoodleDefinition definition) =>
		_definitions.TryGetValue(id, out definition!);
}
