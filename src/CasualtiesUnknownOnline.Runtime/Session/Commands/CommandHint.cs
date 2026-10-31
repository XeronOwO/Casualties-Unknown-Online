namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One player-visible hint about the line being typed: a catalogue KEY and its arguments, never a finished sentence. The reader's own client renders it, so a
/// Chinese client and an English one read the same fact in their own language (the localisation rule the console answers already follow).
/// </summary>
/// <remarks>
/// The hint answers "what is wrong with the line RIGHT NOW" while the player can still fix it — an unknown verb, or a token nothing matches at a position the
/// grammar expects something in. It is distinct from a completion row: rows offer what COULD be typed, a hint explains what was typed.
/// </remarks>
public sealed record CommandHint(string Key, params object?[] Args);
