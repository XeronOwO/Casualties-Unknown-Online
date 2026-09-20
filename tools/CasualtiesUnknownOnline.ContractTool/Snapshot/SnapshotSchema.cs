namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// The snapshot/diff document ids. Bumping one is a deliberate break: a reader
/// refuses a document whose schema it does not know rather than guessing at a
/// shape it was not written for.
/// </summary>
public static class SnapshotSchema
{
	/// <summary>The snapshot document written by `snapshot` and read by `diff`.</summary>
	public const string Snapshot = "cuo.contract-snapshot/1";

	/// <summary>The classified diff document written by `diff --json`.</summary>
	public const string Diff = "cuo.contract-diff/1";
}
