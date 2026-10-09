namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One content definition a mod registers through <see cref="IModContent"/>:
/// the address, the kind and the schema version, and nothing else.
///
/// The type is the kind. A definition of a well-known kind implements this
/// interface through its typed DTO (<see cref="ModItemDefinition"/> and its
/// siblings), whose <see cref="Kind"/> is fixed by the type, so a definition
/// cannot be filed under a kind that does not belong to it and every consumer
/// reads the mod's typed members instead of re-deriving the shape at run time.
/// A mod may implement this interface itself to register a kind of its own; a
/// kind no provider claims stays opaque: the catalog and the console enumerate
/// it and nothing materializes it.
///
/// The framework stores the definition as it was handed over and never
/// interprets its typed members, so a mod registers a definition it does not
/// mutate afterwards.
///
/// The surface is `Stable` rather than `Experimental`: it replaces the stable
/// `TryRegister(id, kind, byte[] data)` shape instead of adding a capability,
/// and its nine implementations are Stable types already, so a lower level here
/// would retract a promise the contract it replaces already carried (decision
/// 247).
/// </summary>
public interface IModContentDefinition
{
	/// <summary>
	/// The mod-scoped content id: a lower-case canonical path segment, unique
	/// within the registering mod. The framework derives the addressable
	/// <c>namespace:id</c> from the registering mod's namespace and this path.
	/// </summary>
	string Id { get; }

	/// <summary>
	/// The content kind. A provided DTO fixes it by type (see
	/// <see cref="ModContentKind"/>); a mod-authored definition declares its own.
	/// </summary>
	string Kind { get; }

	/// <summary>
	/// The mod-owned content schema version, stored verbatim and never migrated
	/// by the framework; it must be positive.
	/// </summary>
	int SchemaVersion { get; }
}
