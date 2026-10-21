using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: this host's world is advancing a layer NOW. The member follows by
/// running the game's own layer regeneration locally — the same coroutine a solo
/// descent runs — instead of being pulled to the menu and re-invited after the
/// host has finished, so every client loads its own new layer in parallel and the
/// start gate releases them together.
/// <para>
/// The edge is sent at the START of the host's regeneration, before the transition
/// reaches the member as a scene report, which is what lets the member tell "the
/// host is descending" from "the host left". <see cref="Twice"/> carries the
/// native two-layer step (the drill pod) so the member advances the same number of
/// layers; <see cref="Generation"/> is the host's kernel generation, the same
/// identity every layer-relative report carries, so a member accepts the edge only
/// for the layer it is actually in.
/// </para>
/// </summary>
[ProtoContract]
public sealed class LayerDescentMsg
{
	/// <summary>True for the native two-layer step (the drill pod): the member runs the same step locally.</summary>
	[ProtoMember(1)]
	public bool Twice { get; set; }

	/// <summary>The host's kernel generation at the descent; null when the host holds no committed run baseline (it then does not send).</summary>
	[ProtoMember(2)]
	public WorldGenerationMsg? Generation { get; set; }
}
