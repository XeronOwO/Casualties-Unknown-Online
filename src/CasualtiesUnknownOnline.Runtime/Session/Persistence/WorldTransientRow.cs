namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// One row of the cut's transient policy table: what an in-flight class is, who
/// owns it, what a cut does with it, and whether CUO can count it at all. The
/// table is documentation AND enforcement — <see cref="WorldTransientPolicy.Rows"/>
/// is the single place a verdict is decided, and the contract test pins every row
/// of the ticket's table so a new in-flight state cannot be added without a
/// verdict and a declared observability.
/// </summary>
/// <param name="Key">The stable key an owner reports its count under.</param>
/// <param name="Owner">The type (or subsystem) that holds the state.</param>
/// <param name="Verdict">What a cut does with the state.</param>
/// <param name="Detection">Whether a CUO owner can count the state at the cut instant (see <see cref="WorldTransientDetection"/>).</param>
/// <param name="Unit">The counted unit, pluralised for the report, e.g. "pending pickup claim(s)".</param>
/// <param name="Note">Why this verdict — the sentence a reviewer needs when the table is revisited.</param>
public sealed record WorldTransientRow(
	string Key,
	string Owner,
	WorldTransientVerdict Verdict,
	WorldTransientDetection Detection,
	string Unit,
	string Note);
