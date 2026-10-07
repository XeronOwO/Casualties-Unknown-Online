using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod content registration surface (Phase 4 Mod API remainder).
/// A mod registers its content definitions — items, recipes, NPC types,
/// skills, map entries and similar static facts — with the framework so other
/// CUO layers can discover and consume them. The framework stores the
/// definitions as opaque bytes and never interprets the mod's payload.
///
/// One rule governs every collection member of every typed payload contract in
/// this assembly: <b>null means empty</b>. A mod that assigns null to a list,
/// dictionary or byte array round-trips an explicit nil — the payload
/// serializer runs no property initializer — and decoding it yields an empty
/// collection, never a definition the binder skips with a logged exception.
/// Both ends of a payload answer for it: the member's own setter coalesces a
/// null write, and the shared decode step behind every contract's
/// <c>FromPayload</c> (<c>ModPayloadCodec</c>) replaces a null collection member
/// of the decoded graph with an empty one, nested contracts included — because a
/// payload that OMITS a member's element never reaches the setter at all. A
/// member that is genuinely required refuses an empty collection with its own
/// message, and <c>ModPayloadNullCollectionTests</c> enumerates every such
/// member from these contracts.
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
	/// Register one content definition with schema version 1. Returns false (with
	/// a framework log) when the mod lacks <see cref="ModPermission.RegisterContent"/>,
	/// the id/kind/payload fails the content policy rails, or the id is already
	/// registered by this mod. Register during <see cref="ICuoMod.Bind"/>.
	/// </summary>
	bool TryRegister(string id, string kind, byte[] data);

	/// <summary>
	/// Register one content definition with a mod-owned schema version. The
	/// framework stores the version verbatim and never migrates the payload, so
	/// the mod owns schema compatibility. Returns false for the same reasons as
	/// the version-1 overload plus a non-positive schema version.
	/// </summary>
	bool TryRegister(string id, string kind, byte[] data, int schemaVersion);

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
