using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Binds <see cref="ModStatusDefinition"/> payloads from shared-content mods
/// into a small, validated static status-descriptor registry. This provider
/// intentionally does not create per-player or per-limb runtime status bags:
/// dynamic status state belongs to a future typed mod-data domain. It gives
/// mods a typed content seam and gives the framework a validated status
/// vocabulary for downstream migration/diagnostics.
/// </summary>
public sealed class GameAdapterStatusContentProvider(
	ILogger<GameAdapterStatusContentProvider> log) : IContentBindingProvider, ICuoService
{
	private readonly ILogger<GameAdapterStatusContentProvider> _log = log;
	private readonly Dictionary<string, ModStatusDefinition> _definitions = [];

	/// <inheritdoc />
	public string Kind => ModContentKind.Status;

	/// <inheritdoc />
	public bool TryBind(ModContentRegistration registration)
	{
		if (!string.Equals(registration.Definition.Kind, Kind, StringComparison.Ordinal))
		{
			return false;
		}

		var definition = ModStatusDefinition.FromPayload(registration.Definition.Data);
		if (definition is null)
		{
			_log.LogWarning(
				"[StatusContent] {ModId}/{Id} payload is not a valid ModStatusDefinition — refused.",
				registration.ModId, registration.Definition.Id);
			return false;
		}

		var id = registration.Definition.Id;
		if (string.IsNullOrWhiteSpace(id))
		{
			_log.LogWarning("[StatusContent] {ModId} registered a status with an empty id — refused.", registration.ModId);
			return false;
		}

		if (!Enum.IsDefined(typeof(ModStatusScope), definition.Scope))
		{
			_log.LogWarning(
				"[StatusContent] {ModId}/{Id} declares an unknown status scope {Scope} — refused.",
				registration.ModId, id, definition.Scope);
			return false;
		}

		if (definition.MoodleId is { Length: > 128 })
		{
			_log.LogWarning(
				"[StatusContent] {ModId}/{Id} MoodleId is longer than the 128-character seam limit — refused.",
				registration.ModId, id);
			return false;
		}

		if (!ValidateLimbMoodleRouting(registration.ModId, id, definition))
		{
			return false;
		}

		if (_definitions.ContainsKey(id))
		{
			_log.LogWarning(
				"[StatusContent] {ModId}/{Id} is already registered by another status-content provider/definition — refused.",
				registration.ModId, id);
			return false;
		}

		_definitions.Add(id, definition);
		_log.LogInformation(
			"[StatusContent] accepted {ModId}/{Id} (scope {Scope}, save {SaveEnabled}; schema {SchemaVersion}).",
			registration.ModId, id, definition.Scope, definition.SaveEnabled, registration.Definition.SchemaVersion);
		return true;
	}

	private bool ValidateLimbMoodleRouting(string modId, string id, ModStatusDefinition definition)
	{
		if (definition.ShowPerLimbMoodles && definition.Scope != ModStatusScope.Limb)
		{
			_log.LogWarning(
				"[StatusContent] {ModId}/{Id} enables ShowPerLimbMoodles on a body-scoped status — refused.",
				modId, id);
			return false;
		}

		if (definition.LimbMoodles is not { Count: > 0 })
		{
			return true;
		}

		if (!definition.ShowPerLimbMoodles || definition.Scope != ModStatusScope.Limb)
		{
			_log.LogWarning(
				"[StatusContent] {ModId}/{Id} has limb moodle bindings without per-limb rows enabled on a limb-scoped status — refused.",
				modId, id);
			return false;
		}

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var binding in definition.LimbMoodles)
		{
			if (binding is null
				|| string.IsNullOrWhiteSpace(binding.LimbName)
				|| string.IsNullOrWhiteSpace(binding.MoodleId)
				|| binding.LimbName.Length > 64
				|| binding.MoodleId.Length > 128)
			{
				_log.LogWarning(
					"[StatusContent] {ModId}/{Id} has an invalid limb moodle binding (empty limb/moodle id or overlong field) — refused.",
					modId, id);
				return false;
			}

			if (!seen.Add(binding.LimbName))
			{
				_log.LogWarning(
					"[StatusContent] {ModId}/{Id} has duplicate limb moodle binding for {Limb} — refused.",
					modId, id, binding.LimbName);
				return false;
			}
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

	/// <summary>Resolve a bound static status descriptor.</summary>
	internal bool TryGetDefinition(string id, out ModStatusDefinition definition) =>
		_definitions.TryGetValue(id, out definition!);
}
