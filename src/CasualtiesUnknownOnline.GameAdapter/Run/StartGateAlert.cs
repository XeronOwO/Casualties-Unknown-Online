namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// One game popup (PlayerCamera.DoAlert, PlayerCamera.cs:2749) captured while
/// the start-gate window holds, replayed after the gate releases.
/// </summary>
internal readonly record struct StartGateAlert(string Text, bool Important);
