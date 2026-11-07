namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The non-refusal observations the mod comparison reports on the host.</summary>
public enum ModAdmissionNoteKind
{
	/// <summary>Both sides list the mod but materialized different content (reported, never refused).</summary>
	ContentParity,

	/// <summary>Both sides list the mod but declare different native bindings under the warn parity policy.</summary>
	BindingWarn,
}
