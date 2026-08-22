using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod content registration surface (Phase 4 Mod API remainder).
/// A mod registers its content definitions — items, recipes, NPC types,
/// skills, map entries and similar static facts — with the framework so other
/// CUO layers can discover and consume them. The framework stores the
/// definitions as opaque bytes and never interprets the mod's payload.
///
/// Registration requires <see cref="ModPermission.RegisterContent"/>: nothing
/// is implicit, and the permission policy already refuses that flag on
/// local-only network modes. The registry is process-local; it does not send
/// content over the wire. Content definitions are part of the mod itself, so
/// the existing Mod API handshake (mod id / version / permissions / mode)
/// is the consistency boundary; a mod that needs client-specific dynamic
/// content must coordinate through <see cref="IModNetwork"/> /
/// <see cref="IModCommands"/> instead.
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
	/// id/kind/payload fails the content policy rails, or the id is already
	/// registered by this mod. Register during <see cref="ICuoMod.Bind"/>.
	/// </summary>
	bool TryRegister(string id, string kind, byte[] data);

	/// <summary>Remove a previously registered definition by id. Returns false when no such id exists.</summary>
	bool TryUnregister(string id);

	/// <summary>True when a definition with this exact id is registered by this mod.</summary>
	bool IsRegistered(string id);

	/// <summary>
	/// A snapshot of this mod's registered definitions (copy — safe to hold).
	/// Each definition's payload is copied on read.
	/// </summary>
	IReadOnlyCollection<ModContentDefinition> Definitions { get; }

	/// <summary>The number of registered definitions for this mod.</summary>
	int Count { get; }
}
