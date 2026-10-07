namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The subject one clone-creation failure window is keyed on: WHICH failure, and for WHOM. The failure's own
/// wording is part of the key on purpose — a window carries the value it bounds, and a scene that has no
/// "Experiment" template is a different fact from a template whose clone carries no Body, so the second is
/// news rather than a repeat of the first. A value type for the same reason <c>ItemDivergenceKey</c> is one:
/// <c>LogRepetitionGuard</c> compares keys with <c>Equals</c>, and a reference type would compare identity,
/// which would make every frame a new failure and bound nothing.
/// </summary>
internal readonly record struct RemoteCloneFailureKey(string Why, ulong SteamId);
