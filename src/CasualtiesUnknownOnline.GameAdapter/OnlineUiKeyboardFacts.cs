using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// One reading of the GAME's own keyboard ownership, in its own file so the adapter class does not grow for it.
/// Two facts are read from the game's own state rather than inferred from the event system: its in-world console
/// is open (<c>ConsoleScript.active</c>; its Enter path executes a command whenever the console is active, with
/// no focus test), or its event system has selected an ACTIVE text field (its settings menu's number fields, its
/// crafting recipe filter). <see cref="INativeInputBlocker.IsTheGamesOwnTextInputFocused"/> is how the plugin asks.
/// </summary>
internal static class OnlineUiKeyboardFacts
{
	/// <summary>
	/// True while the game owns the keyboard. The console check is the console's OWN flag, not the selection:
	/// opening selects its field, but the selection can move while the console stays open (a pause menu, a click
	/// on a non-selectable area), and the game would still execute the Enter. The text-field check also requires
	/// the selected object to be active in hierarchy, which is what makes it a lifetime fact: the console selects
	/// its field when it opens and never clears the selection when it closes, so after one use of the game console
	/// the event system keeps naming a field whose canvas is inactive — asking only "does the selected object
	/// carry a text field" kept the CUO Enter/Slash opener refused until something else took the selection
	/// (batch `20261018-t` row E2).
	/// </summary>
	internal static bool IsTheGameUsingTheKeyboard()
	{
		if (ConsoleScript.instance != null && ConsoleScript.instance.active) // Unity object — ==
		{
			return true;
		}

		// The settings menu's key-bind capture takes a raw key with NO text field and NO console open: the row's
		// button starts a coroutine that binds the first key it sees pressed (SettingsMenu.cs:196-213), and that
		// state is invisible to both checks below — the opener's own defect: pressing Enter to BIND Enter also
		// opened the CUO console. The capture has no field of its own, but it is the ONLY writer of the "..." text
		// (SettingsMenu.cs:198, one occurrence in the whole file) and it writes it into the settings row that is
		// waiting, so an open settings menu carrying that text IS the capture. The read is bounded to the key
		// presses that ask (the opener's edge), not to a per-frame scan.
		if (IsAKeybindCaptureWaiting())
		{
			return true;
		}

		var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
		return selected != null // Unity object — ==
			&& selected.activeInHierarchy
			&& (selected.GetComponent<TMP_InputField>() != null || selected.GetComponent<InputField>() != null);
	}

	/// <summary>
	/// Whether the settings menu is waiting for a key. The capture writes the literal <c>"..."</c> into the row it is
	/// binding (<c>SettingsMenu.cs:198</c>, the only occurrence in that file) and keeps it there until a key arrives, so
	/// an active settings menu carrying that text is the capture. Nothing else in the game writes that marker into a
	/// settings row, and the scan is bounded to an OPEN settings menu.
	/// </summary>
	private static bool IsAKeybindCaptureWaiting()
	{
		var menu = Object.FindObjectOfType<SettingsMenu>();
		if (menu == null || !menu.gameObject.activeInHierarchy) // Unity object — ==
		{
			return false;
		}

		foreach (var text in menu.GetComponentsInChildren<TMP_Text>(true))
		{
			if (text != null && text.text == "...") // Unity object — ==
			{
				return true;
			}
		}

		return false;
	}
}
