namespace CasualtiesUnknownOnline.Runtime.Protocol;

/// <summary>
/// Enemy proximity side-effect kinds carried by <c>EnemyEffectMsg</c>. Each
/// kind is one discrete trigger of a local-body mutation that must travel as a
/// dedicated event (never the 1 Hz character snapshot): the affected player
/// reports the post-effect terminal state; the host adopts it and relays.
/// Values start at 1 — protobuf omits zero, and Kind is never "unset".
/// </summary>
public enum EnemyEffectKind : byte
{
	/// <summary>ElderThornback 1 s horror/stamina tick inside the 45/101.25-unit fields (ElderThornbackBehaviour.cs:43-101).</summary>
	ElderHorrorTick = 1,

	/// <summary>ElderThornback died within 45 units of the player — horror cleared plus the happiness/caffeine reward (ElderThornbackBehaviour.cs:28-40).</summary>
	ElderHorrorDefeat = 2,

	/// <summary>Xaloris septic tick inside 5.5 units (XalorisScript.cs:23-31).</summary>
	XalorisSepticTick = 3,

	/// <summary>GrabberPlant grabbed the player (GrabberPlant.cs:75-90).</summary>
	GrabberGrabbed = 4,
}
