namespace CasualtiesUnknownOnline.Runtime.Session.Chat;

/// <summary>
/// One chat line in the local recent-chat buffer. Immutable and UI-oriented:
/// the sender SteamId is resolved to a persona name by the Online UI, the text
/// is the host/author-final string.
/// </summary>
public sealed record ChatLine(ulong SenderSteamId, string Text);
