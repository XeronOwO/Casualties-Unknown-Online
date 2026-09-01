namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The Runtime → Game Adapter boundary for turning an opaque mod content
/// registration into concrete game content. Each provider owns one content
/// kind (item, recipe, tile, ...) and is allowed to know the game/Unity types
/// that the specific kind requires. The Runtime content binder is agnostic to
/// providers and only routes entries by kind.
/// </summary>
public interface IContentBindingProvider
{
	/// <summary>The exact content kind this provider handles (see <see cref="ModContentKind"/>).</summary>
	string Kind { get; }

	/// <summary>
	/// Bind one content registration. Returns false when the payload is invalid,
	/// the id is a duplicate, or the provider cannot accept the entry. The
	/// provider is responsible for deciding whether "not ready yet" should be a
	/// retryable state (for example, waiting for the vanilla item table).
	/// </summary>
	bool TryBind(ModContentRegistration registration);
}
