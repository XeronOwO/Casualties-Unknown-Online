using System.Collections.Generic;
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

	/// <summary>Container entries currently expanded in the remote-inventory view, keyed by owner SteamId + item instance id.</summary>
	internal HashSet<string> ExpandedContainers { get; } = [];

	internal bool LogLevelOptionsOpen;

	internal bool LanguageOptionsOpen;

	internal bool ColorOptionsOpen;

	internal string ProfileNameInput = "";

	internal string? ProfileStatus;

	internal string ConsoleInput = "";

	internal bool ProfileStatusIsError;

	internal OnlineUiTransportMode TransportMode = OnlineUiTransportMode.Steam;
}
