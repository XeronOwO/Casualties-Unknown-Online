using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One mod's write guard over its own slice of the framework-wide content
/// registry: the permission gate, the id/kind/schema rails and the count cap are
/// decided here, the definitions themselves live in <see cref="ModContentStore"/>
/// (their one owner, so the readers outside this domain see the same set), and the
/// object a mod hands over is kept as it is — no copy — so a mod registers a
/// definition it does not mutate afterwards.
/// </summary>
internal sealed class ModContentAdapter(ModManifest manifest, ModContentStore store, ILogger log) : IModContent
{
	public bool CanRegister => ModPermissionGate.HasPermission(manifest, ModPermission.RegisterContent);

	public int Count => store.CountOf(manifest.Id);

	public IReadOnlyCollection<IModContentDefinition> Definitions => store.DefinitionsOf(manifest.Id);

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

		if (store.IsRegistered(manifest.Id, id))
		{
			log.LogWarning("[Mods] {ModId}/{Id} is already registered as content — the duplicate is refused.",
				manifest.Id, id);
			return false;
		}

		if (!ModContentPolicy.CanAdd(store.CountOf(manifest.Id)))
		{
			log.LogWarning("[Mods] {ModId} reached the {Cap}-definition content cap — {Id} refused.",
				manifest.Id, ModContentPolicy.MaxDefinitionsPerMod, id);
			return false;
		}

		store.Add(manifest.Id, manifest.Namespace, definition);
		log.LogInformation("[Mods] {ModId} registered content {Id} ({Kind}, schema {SchemaVersion}, {DefinitionType}).",
			manifest.Id, id, definition.Kind, definition.SchemaVersion, definition.GetType().Name);
		return true;
	}

	public bool TryUnregister(string id) => store.Remove(manifest.Id, id);

	public bool IsRegistered(string id) => store.IsRegistered(manifest.Id, id);
}
