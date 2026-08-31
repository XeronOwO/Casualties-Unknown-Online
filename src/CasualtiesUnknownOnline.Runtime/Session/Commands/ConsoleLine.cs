namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One immutable line in the command-console output buffer. The buffer is owned
/// by <see cref="CommandConsoleService"/>; the UI only reads this projection.
/// </summary>
public sealed record ConsoleLine(ConsoleLineKind Kind, string Text);
