namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The kind of a node in a console command tree: either a literal subcommand
/// branch or a typed argument.
/// </summary>
internal enum CommandNodeKind
{
	Argument,
	Literal,
}
