using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// Mod content half of <see cref="ModService"/> (Phase 4 Mod API remainder).
/// Each mod gets a per-id registry of opaque content definitions. Registration
/// requires <see cref="ModPermission.RegisterContent"/> — nothing is implicit —
/// and the registry is process-local by design: content is part of the mod
/// itself, so the Mod API handshake already provides the consistency boundary
/// and no content bytes cross the wire. The plugin and future native-content
/// consumers read the framework-wide view through <see cref="IModContentControl"/>.
/// </summary>
public sealed partial class ModService : IModContentControl
{
	public IReadOnlyList<ModContentRegistration> Entries =>
		[.. _mods.SelectMany(m => m.Context.ContentAdapter.Definitions.Select(d =>
			new ModContentRegistration(m.Manifest.Id, d)))];

	// ---- Per-mod content adapter ----

	/// <summary>
	/// The per-mod content registry: a small definition list scoped by
	/// construction to one mod id. Registration failures are logged and refused
	/// (missing permission, invalid id/kind/data, duplicate id, count cap); the
	/// stored payloads are defensive copies and every read returns another copy.
	/// </summary>
	private sealed class ModContentAdapter(ModService owner, ModManifest manifest) : IModContent
	{
		private readonly List<ModContentDefinition> _definitions = [];

		public bool CanRegister => ModService.HasPermission(manifest, ModPermission.RegisterContent);

		public int Count => _definitions.Count;

		public IReadOnlyCollection<ModContentDefinition> Definitions => [.. _definitions];

		public bool TryRegister(string id, string kind, byte[] data)
		{
			if (!CanRegister)
			{
				owner.LogMissingPermission(manifest.Id, "RegisterContent");
				return false;
			}

			if (!ModContentPolicy.IsValidId(id))
			{
				owner._log.LogWarning("[Mods] {ModId} tried to register content with an invalid id {Id} — refused.",
					manifest.Id, id);
				return false;
			}

			if (!ModContentPolicy.IsValidKind(kind))
			{
				owner._log.LogWarning("[Mods] {ModId} tried to register content {Id} with an invalid kind {Kind} — refused.",
					manifest.Id, id, kind);
				return false;
			}

			if (!ModContentPolicy.IsValidData(data))
			{
				owner._log.LogWarning("[Mods] {ModId} tried to register content {Id} with a {Length}-byte payload; the cap is {Cap} bytes — refused.",
					manifest.Id, id, data?.Length ?? 0, ModContentPolicy.MaxDefinitionBytes);
				return false;
			}

			if (_definitions.Any(d => d.Id == id))
			{
				owner._log.LogWarning("[Mods] {ModId}/{Id} is already registered as content — the duplicate is refused.",
					manifest.Id, id);
				return false;
			}

			if (!ModContentPolicy.CanAdd(_definitions.Count))
			{
				owner._log.LogWarning("[Mods] {ModId} reached the {Cap}-definition content cap — {Id} refused.",
					manifest.Id, ModContentPolicy.MaxDefinitionsPerMod, id);
				return false;
			}

			_definitions.Add(new ModContentDefinition(id, kind, data));
			owner._log.LogInformation("[Mods] {ModId} registered content {Id} ({Kind}, {Length} bytes).",
				manifest.Id, id, kind, data.Length);
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
}
