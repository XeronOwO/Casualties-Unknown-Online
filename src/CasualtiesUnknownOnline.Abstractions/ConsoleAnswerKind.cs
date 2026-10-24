namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// How a console command's answer reads: a completed action or a refusal. The console colours the line by this
/// kind, so a verb says what happened instead of a reader guessing from the text — the defect this type exists for
/// is a refusal ("unknown rule", "not permitted", "no such member") rendered in the success colour because the
/// handler could only return a string.
/// </summary>
public enum ConsoleAnswerKind
{
	/// <summary>The command did what it was asked.</summary>
	Success,

	/// <summary>The command refused: a permission, an argument, an unknown target or a rule's own answer.</summary>
	Refusal,
}
