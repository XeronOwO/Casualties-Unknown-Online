using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Mutable local UI state for the Online UI window. This is presentation state
/// only; it is owned by <see cref="OnlineUiWindow"/> and never crosses into the
/// Runtime or the wire.
/// </summary>
internal sealed class OnlineUiWindowState
{
	internal bool Visible;

	internal OnlineUiPage Page = OnlineUiPage.Home;

	internal string LobbyIdInput = "";

	internal string? Error;

	internal Vector2 Scroll;

	internal ulong? ExpandedMember;
}
