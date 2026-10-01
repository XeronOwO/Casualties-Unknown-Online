namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// One session-liveness notice for the Online UI's status line: the text is
/// already rendered by the runtime through <c>ILocalizationService</c> at the
/// moment the fact happens, so the UI only displays it. A notice is a transient
/// line, never re-read — there is nothing a later language switch could
/// re-render.
/// </summary>
public readonly record struct SessionNotice(string Text);
