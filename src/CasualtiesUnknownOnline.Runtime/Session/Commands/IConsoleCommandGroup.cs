namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Marks a class whose methods carry <see cref="ConsoleCommandAttribute"/> verbs, so the command console
/// registers every declared verb WITHOUT a hand-maintained list.
///
/// <para>
/// The rule (<c>docs/development/agent-reference.md</c>): for large families of similar registration code
/// (commands, handlers, providers, packets) prefer discoverable attribute + reflection registration over
/// hard-coded linear registration. The verbs themselves were already attribute-declared; what was still
/// hard-coded was which groups exist. A class implementing this marker is registered in the container, and
/// the console asks for <c>IEnumerable&lt;IConsoleCommandGroup&gt;</c> and calls its attribute scan once per
/// group — so a new verb group is one registration, never an edit to the console.
/// </para>
/// </summary>
internal interface IConsoleCommandGroup
{
}
