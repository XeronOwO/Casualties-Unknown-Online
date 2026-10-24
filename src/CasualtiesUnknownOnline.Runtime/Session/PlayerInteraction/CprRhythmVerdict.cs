namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// What the CPR control tells its operator about the press they just made (ticket `remote-medical-cpr.md`, stage 4's
/// intelligence rule). The reference's handler feeds back "good timing" when the press lands in its band and names the
/// miss otherwise; a low-intelligence operator keeps the miss and loses the affirmation, which is the whole of its
/// "harder screen" — the physiology is identical.
/// </summary>
public enum CprRhythmVerdict
{
	/// <summary>No verdict yet: the control shows its guide text.</summary>
	None = 0,

	/// <summary>Inside the band: the press earned its full gain.</summary>
	Good = 1,

	/// <summary>Faster than the fast edge: a shove, half gain and a rib.</summary>
	TooFast = 2,

	/// <summary>Slower than the slow edge: no perfusion at all.</summary>
	TooSlow = 3,
}
