namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One completion candidate with an optional human-readable description. The UI
/// can show the description as a hint/tooltip row; the input session uses only
/// <see cref="Text"/> for the actual insertion.
/// </summary>
public sealed record CommandSuggestion(string Text, string? Description = null);
