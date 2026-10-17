using System.Reflection;
using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Extends the game's own <c>heal</c> console command with the two things it does not do: putting a severed limb
/// back, and clearing the hollow debuff.
/// <para>
/// The user's request names this command ("the game's own <c>heal</c> command does not restore severed limbs and
/// does not remove the hollow debuff"), and KrokMP re-registers the same name, so this is that surface and not
/// CUO's own item-based <c>/heal</c>. The native entry writes the limb and body fields of the LOCAL body and ends
/// with its own console line; nothing in CUO patched that class before, so this wraps the registered command
/// rather than changing an existing hook.
/// </para>
/// <para>
/// It wraps the command's own action instead of re-implementing the command: the native body stays the one
/// authority for the fields it already heals, and CUO's effects run after it, reporting on the console the player
/// is already reading. They run on the client whose body it is — the command already targets
/// <c>PlayerCamera.main.body</c> — and the existing sync carries them out.
/// </para>
/// </summary>
[HarmonyPatch(typeof(ConsoleScript), "RegisterAllCommands")]
internal static class ConsoleScriptHealHollowPatch
{
	private const string HealCommand = "heal";

	/// <summary>
	/// The command instance already wrapped. The game registers its commands exactly once — the only caller sits
	/// under a "commands are empty" test and nothing in the assembly clears or replaces the static list — so this
	/// guard is not for a re-registration: it keeps a second Harmony pass over the same instance from stacking a
	/// second wrapper on it.
	/// </summary>
	private static object? _wrapped;

	/// <summary>
	/// The console's own line writer, which is PRIVATE on the game's class. The adapter's business is absorbing
	/// the game's private surface, and there is no public console API to report through — the native command's own
	/// output goes here, so CUO's added effects report in the same place the player is already reading. Resolved
	/// once, and a missing member leaves the effect silent rather than failing the command.
	/// </summary>
	private static MethodInfo? _logToConsole;

	private static void Postfix()
	{
		var command = ConsoleScript.SearchExact(HealCommand);
		if (command == null || ReferenceEquals(command, _wrapped))
		{
			return;
		}

		var native = command.action;
		command.action = args =>
		{
			try
			{
				native?.Invoke(args);
			}
			finally
			{
				// The effects run even when the native body threw: it has already written the limb and blood
				// fields before its last two statements, so a failed console line must not leave a healed player
				// still hollow, or with an arm missing.
				var camera = PlayerCamera.main;
				var body = camera == null ? null : camera.body; // Unity objects — ==
				if (body != null && LimbRestore.Apply(body) is { } restoredLimb)
				{
					// ONE report for ONE action. The limb-state message carries EVERY limb of the owner and the
					// receiver replaces its whole set, so a single message from any cleared limb reconstructs the
					// body exactly; reporting per limb would send N identical messages for one heal.
					PatchBridge.Impl?.OnLimbStateEvent(restoredLimb);
					Report("Restored the severed limbs.");
				}

				if (body != null && HollowClear.Apply(body))
				{
					Report("Cleared the hollow.");
				}
			}
		};

		_wrapped = command;
	}

	private static void Report(string text)
	{
		_logToConsole ??= typeof(ConsoleScript).GetMethod("LogToConsole", BindingFlags.Instance | BindingFlags.NonPublic);
		if (_logToConsole != null && ConsoleScript.instance != null) // Unity object — ==
		{
			_logToConsole.Invoke(ConsoleScript.instance, [text]);
		}
	}
}
