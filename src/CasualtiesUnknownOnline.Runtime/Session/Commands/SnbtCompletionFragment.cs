namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>One completion fragment: the text that replaces the segment being typed, and the hint row.</summary>
internal readonly record struct SnbtCompletionFragment(string Text, string? Description);
