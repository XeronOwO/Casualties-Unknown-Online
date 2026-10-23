namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// How a console command's authority is decided, declared where the verb is classified so the
/// command's own gate and the console's completion/help filter can never disagree.
///
/// <para>
/// <see cref="Open"/> means the console's <c>CommandPermission</c> is the whole rule: the verb runs
/// for every caller the role check admits. <see cref="Cheat"/> means the verb is one of the cheat
/// family's — <c>Anyone</c> at the console and admitted only through <see cref="CheatVerbGate"/>
/// inside the handler — so the console hides its word, its arguments and its help row from a caller
/// the gate would refuse, while typing the line still reaches the handler and is refused by name.
/// The hidden word is a courtesy; the handler's gate is the enforcement.
/// </para>
/// </summary>
public enum ConsoleCommandGate
{
	/// <summary>The command's permission is the whole authority.</summary>
	Open,

	/// <summary>The command belongs to the cheat family: admitted by the host's switch and its roster, implemented by the handler's own gate.</summary>
	Cheat,
}
