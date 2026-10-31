namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The console's output, as the collaborators that contribute to it see it. A collaborator that prints (the relay's answer printer, for instance) writes
/// through this seam instead of holding the whole console service, which is how the service stays under the aggregate-size line.
/// </summary>
internal interface IConsoleLineSink
{
	/// <summary>One line whose sentence comes from the catalogue: the client renders the key in its own language, falling back to the text.</summary>
	void AddKeyedLine(string key, string text, ConsoleLineKind kind, params string[] args);

	/// <summary>One line with no catalogue key (a sentence assembled from free data, or the history-only part of a report).</summary>
	void AddLine(string text, ConsoleLineKind kind, bool notifiable);
}
