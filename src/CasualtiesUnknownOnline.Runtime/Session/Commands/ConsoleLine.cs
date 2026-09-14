namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One immutable line in the command-console output buffer. The buffer is owned
/// by <see cref="CommandConsoleService"/>; the UI only reads this projection.
/// <see cref="CreatedAtUtcTicks"/> lets the UI apply the fade policy without
/// owning Runtime timing state.
///
/// <see cref="Notifiable"/> separates "a line the player should be interrupted
/// by" from "a line that belongs in the history": the closed console shows only
/// its newest few lines as transient notifications, and a report that pushes one
/// line per lost item would flush every other notice out of that window while
/// saying the same thing five times over. A report's detail lines are therefore
/// history-only; its headline is the notification
/// (<see cref="ConsoleNotificationPolicy"/> applies the rule).
/// </summary>
public sealed record ConsoleLine(ConsoleLineKind Kind, string Text, long CreatedAtUtcTicks, bool Notifiable = true);
