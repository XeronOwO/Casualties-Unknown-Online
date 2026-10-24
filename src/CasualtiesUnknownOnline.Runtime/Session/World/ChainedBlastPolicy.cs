namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Whether a mine the host's blast damaged will fire its OWN explosion. The game's mine chains from
/// <c>MineScript.OnDestroy</c> when the entity's health is below half and it has not fired yet
/// (<c>MineScript.cs</c>: `if (this.build.health &lt; 0.5f &amp;&amp; !this.exploded)`), and that destroy runs later,
/// outside the scope that captured the first blast — so its victims would be neither captured nor marked and the host
/// would roll a second drop set for a death the member's own client already rolled. The host therefore replays it
/// inside the same scope, and this is the rule that says which mines that replay takes.
/// </summary>
public static class ChainedBlastPolicy
{
	/// <summary>The mine's own firing line (<c>MineScript.cs</c>'s <c>0.5f</c>); a mine below it explodes on destroy.</summary>
	public const float FiringLine = 0.5f;

	/// <summary>
	/// True when this damaged entity is a mine that has not fired yet and is below its firing line — the shape the host
	/// must replay inside the scope, and mark fired so its own destroy does not explode it a second time.
	/// </summary>
	public static bool ShouldReplay(bool hasMineScript, float health, bool alreadyExploded) =>
		hasMineScript && health < FiringLine && !alreadyExploded;
}
