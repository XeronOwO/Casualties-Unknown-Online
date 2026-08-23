namespace CasualtiesUnknownOnline.Runtime.Session.Chat;

/// <summary>
/// The pure text-chat policy: the only validation surface shared by the
/// sender-side UI path and the host's receive/relay path. The limit is a UI
/// friendly cap for one line of text; the host uses the same rule so a guest
/// cannot flood an oversized line into the other peers.
/// </summary>
internal static class ChatPolicy
{
	internal const int MaxLength = 200;

	internal static bool IsValid(string? text) =>
		!string.IsNullOrWhiteSpace(text) && text!.Length <= MaxLength;

	internal static string Normalize(string text) => text.Trim();
}
