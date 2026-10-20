using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: this member is out of the world and its way out was the host's own world exit
/// (a layer change pulls every member to the menu with it), so it asks to be taken back in.
/// <para>
/// The message carries no content on purpose. What it adds to the host is the one fact the host
/// cannot establish itself — the REASON the member is in the menu — and that reason is the
/// member's own: a member that left the world on its own asked for the menu and must stay there,
/// while a member the host pulled out is waiting for the entry instruction it may never have
/// received. The transport sender is the identity, and every fact the host judges the request
/// against (membership, the member's scene state, its own world and run baseline) is the host's
/// own. The answer is the ordinary targeted <c>WorldJoin</c> plus its run-baseline checkpoint —
/// the same instruction the click and the world-entry edge send.
/// </para>
/// </summary>
[ProtoContract]
public sealed class ReentryRequestMsg
{
}
