using CasualtiesUnknownOnline.Runtime.OnlineUi;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// Read-only access to the game's own UI, as plain facts (the Online UI art/controls overhaul, S1).
/// One call builds the CUO canvas under the game's main canvas, instantiates the game's own
/// settings-row prefabs into it, and reads the four unknowns — the game's UI font, a settings row's
/// image style, the chrome's image styles and <c>PlayerCamera.uiScale</c>. It changes nothing in the
/// game: no setting is written and no input is taken.
///
/// <para>
/// The probe's own scene objects live exactly as long as they are needed: the inactive canvas and the
/// rows it loaded are kept between attempts (two of the four facts appear at different moments, and
/// rebuilding them on every retry would be waste), and they are destroyed the moment a reading is
/// complete — or, at the latest, when the adapter is disposed. Nothing the probe created outlives the
/// adapter.
/// </para>
///
/// <para>
/// The contract is deliberately total: the call never throws at the caller's frame callback. An
/// attempt that found no canvas (the game is still starting) returns <c>CanvasAttached == false</c>,
/// and a game update that moved a prefab is reported as a missing part plus a <c>Note</c> line. How
/// long to keep asking is the caller's decision
/// (<see cref="OnlineUiNativeFactsCapturePolicy"/>).
/// </para>
/// </summary>
public interface IOnlineUiNativeFactsQuery
{
	/// <summary>One probe attempt: find or build the host, read what is readable, and report it.</summary>
	OnlineUiNativeFacts Capture();
}
