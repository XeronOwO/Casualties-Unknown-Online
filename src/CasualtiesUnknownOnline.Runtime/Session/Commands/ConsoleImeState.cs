namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Unity-free snapshot of the legacy Input Manager's IME composition state.
/// The overlay polls <c>Input.compositionString</c>, stores the current
/// composition here so the overlay can suppress editor input while composing.
/// Kept separate from
/// <see cref="ConsoleInputSession"/> so IME policy stays testable without
/// UnityEngine.
/// </summary>
public sealed class ConsoleImeState
{
	private string _composition = "";

	/// <summary>The current composition string, or empty when no IME composition is active.</summary>
	public string Composition => _composition;

	/// <summary>True while an IME composition is in progress.</summary>
	public bool IsComposing => _composition.Length > 0;

	/// <summary>Refreshes the composition snapshot from Unity's legacy Input API.</summary>
	public void Update(string? composition) => _composition = composition ?? "";

	/// <summary>Clears the composition snapshot (console open/close or completion reset).</summary>
	public void Clear() => Update(null);
}
