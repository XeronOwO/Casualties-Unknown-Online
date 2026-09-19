namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// What the host's own facts say about a start whose target verdict just arrived
/// (see <see cref="MedicalStartRecheck"/>, which is the only producer).
/// </summary>
internal enum MedicalStartRecheckOutcome
{
	/// <summary>Nothing changed over the window — open the session.</summary>
	Proceed,

	/// <summary>The operator left while the target answered: nobody is waiting, so the start is dropped without an answer.</summary>
	OperatorGone,

	/// <summary>Refused, with the reason naming what changed.</summary>
	Reject,
}
