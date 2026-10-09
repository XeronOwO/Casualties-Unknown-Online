using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod content registration surface (Phase 4 Mod API remainder).
/// A mod registers its content definitions — items, recipes, NPC types,
/// skills, map entries and similar static facts — with the framework so other
/// CUO layers can discover and consume them. The definition carries its own id,
/// kind and schema version (<see cref="IModContentDefinition"/>), so the kind
/// travels with the object and the registry reads nothing but the typed members
/// the mod handed it.
///
/// Registration requires <see cref="ModPermission.RegisterContent"/>: nothing
/// is implicit, and the permission policy already refuses that flag on
/// local-only network modes. The registry is process-local; it does not send
/// content over the wire. Content definitions are part of the mod itself, so
/// the existing Mod API handshake (mod id / version / permissions / mode)
/// is the consistency boundary; a mod that needs client-specific dynamic
/// content must coordinate through <see cref="IModNetwork"/> /
/// <see cref="IModCommands"/> instead.
///
/// The registry stores the definition object as it was handed over — it makes
/// no copy — so a mod registers a definition it does not mutate afterwards.
/// </summary>
public interface IModContent
{
	/// <summary>
	/// True when this mod copy declares <see cref="ModPermission.RegisterContent"/>.
	/// Every registration method also checks and logs this before acting.
	/// </summary>
	bool CanRegister { get; }

	/// <summary>
	/// Register one content definition. Returns false (with a framework log)
	/// when the mod lacks <see cref="ModPermission.RegisterContent"/>, the
	/// definition is null, its id or kind fails the content policy rails, its
	/// schema version is not positive, or the id is already registered by this
	/// mod. A mod registers during <see cref="ICuoMod.Bind"/>; the framework
	/// registers a mod's <see cref="ModContentAttribute"/> declarations through
	/// this same method just BEFORE that bind, so a bind that also registers sees
	/// them and a duplicate id is refused on the code side.
	/// </summary>
	bool TryRegister(IModContentDefinition definition);

	/// <summary>Remove a previously registered definition by id. Returns false when no such id exists.</summary>
	bool TryUnregister(string id);

	/// <summary>True when a definition with this exact id is registered by this mod.</summary>
	bool IsRegistered(string id);

	/// <summary>
	/// A snapshot of this mod's registered definitions: the instances the mod
	/// registered, not copies of them.
	/// </summary>
	IReadOnlyCollection<IModContentDefinition> Definitions { get; }

	/// <summary>The number of registered definitions for this mod.</summary>
	int Count { get; }
}
