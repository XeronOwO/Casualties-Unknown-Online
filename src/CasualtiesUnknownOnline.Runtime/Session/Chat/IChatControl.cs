using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Chat;

/// <summary>
/// The chat domain surface used by the Online UI and (through the world
/// channel) by packet handlers. It owns a bounded recent-message buffer and
/// the send path; it does not touch Unity or Steam internals.
/// </summary>
public interface IChatControl
{
	/// <summary>Read-only recent chat lines, oldest first (bounded).</summary>
	IReadOnlyList<ChatLine> Recent { get; }

	/// <summary>Raised when a new chat line enters the local buffer (own send or a received line).</summary>
	event Action<ChatLine>? MessageReceived;

	/// <summary>
	/// Try to send one local chat line. Returns false when the session is not
	/// active or the text is empty/too long; the message is added to the local
	/// buffer before the wire send so the author sees it immediately.
	/// </summary>
	bool TrySend(string text);
}
