using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod content registry: a small definition list scoped by
/// construction to one mod id. Registration failures are logged and refused
/// (missing permission, null definition, invalid id/kind/schema version,
/// duplicate id, count cap). The registry keeps the definition object the mod
/// handed over — it takes no copy — so a mod registers a definition it does not
/// mutate afterwards.
/// </summary>
internal sealed class ModContentAdapter(ModManifest manifest, ILogger log) : IModContent
{
	private readonly List<IModContentDefinition> _definitions = [];

	public bool CanRegister => ModPermissionGate.HasPermission(manifest, ModPermission.RegisterContent);

	public int Count => _definitions.Count;

	public IReadOnlyCollection<IModContentDefinition> Definitions => [.. _definitions];

	public bool TryRegister(IModContentDefinition definition)
	{
		if (!ModPermissionGate.Try(log, manifest, ModPermission.RegisterContent))
		{
			return false;
		}

		if (definition is null)
		{
			log.LogWarning("[Mods] {ModId} tried to register a null content definition — refused.", manifest.Id);
			return false;
		}

		var id = definition.Id;
		if (!ModContentPolicy.IsValidId(id))
		{
			log.LogWarning("[Mods] {ModId} tried to register content with an invalid id {Id} — refused.",
				manifest.Id, id);
			return false;
		}

		if (!ModContentPolicy.IsValidKind(definition.Kind))
		{
			log.LogWarning("[Mods] {ModId} tried to register content {Id} with an invalid kind {Kind} — refused.",
				manifest.Id, id, definition.Kind);
			return false;
		}

		if (!ModContentPolicy.IsValidSchemaVersion(definition.SchemaVersion))
		{
			log.LogWarning("[Mods] {ModId} tried to register content {Id} with invalid schema version {SchemaVersion} — refused.",
				manifest.Id, id, definition.SchemaVersion);
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

		_definitions.Add(definition);
		log.LogInformation("[Mods] {ModId} registered content {Id} ({Kind}, schema {SchemaVersion}, {DefinitionType}).",
			manifest.Id, id, definition.Kind, definition.SchemaVersion, definition.GetType().Name);
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
