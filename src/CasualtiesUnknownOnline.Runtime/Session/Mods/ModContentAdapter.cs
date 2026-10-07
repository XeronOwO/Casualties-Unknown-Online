using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod content registry: a small definition list scoped by
/// construction to one mod id. Registration failures are logged and refused
/// (missing permission, invalid id/kind/data, duplicate id, count cap); the
/// stored payloads are defensive copies and every read returns another copy.
/// </summary>
internal sealed class ModContentAdapter(ModManifest manifest, ILogger log) : IModContent
{
	private readonly List<ModContentDefinition> _definitions = [];

	public bool CanRegister => ModPermissionGate.HasPermission(manifest, ModPermission.RegisterContent);

	public int Count => _definitions.Count;

	public IReadOnlyCollection<ModContentDefinition> Definitions => [.. _definitions];

	public bool TryRegister(string id, string kind, byte[] data) =>
		TryRegister(id, kind, data, 1);

	public bool TryRegister(string id, string kind, byte[] data, int schemaVersion)
	{
		if (!ModPermissionGate.Try(log, manifest, ModPermission.RegisterContent))
		{
			return false;
		}

		if (!ModContentPolicy.IsValidId(id))
		{
			log.LogWarning("[Mods] {ModId} tried to register content with an invalid id {Id} — refused.",
				manifest.Id, id);
			return false;
		}

		if (!ModContentPolicy.IsValidKind(kind))
		{
			log.LogWarning("[Mods] {ModId} tried to register content {Id} with an invalid kind {Kind} — refused.",
				manifest.Id, id, kind);
			return false;
		}

		if (!ModContentPolicy.IsValidSchemaVersion(schemaVersion))
		{
			log.LogWarning("[Mods] {ModId} tried to register content {Id} with invalid schema version {SchemaVersion} — refused.",
				manifest.Id, id, schemaVersion);
			return false;
		}

		if (!ModContentPolicy.IsValidData(data))
		{
			log.LogWarning("[Mods] {ModId} tried to register content {Id} with a {Length}-byte payload; the cap is {Cap} bytes — refused.",
				manifest.Id, id, data?.Length ?? 0, ModContentPolicy.MaxDefinitionBytes);
			return false;
		}

		if (_definitions.Any(d => d.Id == id))
		{
			log.LogWarning("[Mods] {ModId}/{Id} is already registered as content — the duplicate is refused.",
				manifest.Id, id);
			return false;
		}

		if (!ModContentPolicy.CanAdd(_definitions.Count))
		{
			log.LogWarning("[Mods] {ModId} reached the {Cap}-definition content cap — {Id} refused.",
				manifest.Id, ModContentPolicy.MaxDefinitionsPerMod, id);
			return false;
		}

		var definition = new ModContentDefinition(id, kind, data, schemaVersion);
		_definitions.Add(definition);
		log.LogInformation("[Mods] {ModId} registered content {Id} ({Kind}, schema {SchemaVersion}, {Length} bytes).",
			manifest.Id, id, kind, definition.SchemaVersion, data.Length);
		return true;
	}

	public bool TryUnregister(string id)
	{
		var index = _definitions.FindIndex(d => d.Id == id);
		if (index < 0)
		{
			return false;
		}

		_definitions.RemoveAt(index);
		return true;
	}

	public bool IsRegistered(string id) => _definitions.Any(d => d.Id == id);
}
