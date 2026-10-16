using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// One reading of the GAME's own keyboard ownership, in its own file so the adapter class does not grow for it:
/// the game's in-world console executes a command on Enter while its field is focused, its settings menu binds
/// whichever key the player presses, and its crafting panel suppresses its own hotkey while the recipe filter is
/// focused. <see cref="INativeInputBlocker.IsTheGamesOwnTextInputFocused"/> is how the plugin asks.
/// </summary>
internal static class OnlineUiKeyboardFacts
{
	/// <summary>True while the selected object of the game's event system carries a text field.</summary>
	internal static bool IsATextFieldFocused()
	{
		var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
		return selected != null // Unity object — ==
			&& (selected.GetComponent<TMP_InputField>() != null || selected.GetComponent<InputField>() != null);
	}
}
