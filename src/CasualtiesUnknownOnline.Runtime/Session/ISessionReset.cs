namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The ONE session-teardown contract: a service that owns state belonging to a
/// session implements this interface and subscribes <see cref="ResetSessionState"/>
/// to <see cref="ISessionControl.SessionEnded"/>, unsubscribing the same method
/// when it is disposed.
///
/// <para>
/// The stage sits beside <c>ICuoService</c> instead of inside it: a CUO service
/// lives for the whole process while sessions come and go, so only a
/// session-scoped owner takes part. Before this contract the tree spelled the same
/// stage four ways (<c>Reset</c>, <c>ResetForSessionEnd</c>,
/// <c>ResetSessionState</c>, <c>OnSessionEnded</c>) and nothing asserted that a
/// session-scoped service had one at all. <c>SessionLifecycleGateTests</c> now
/// requires that every subscription to the session-end edge names this method, that
/// it carries its unbind half, and that the two retired session spellings do not
/// come back; the observable consequence is the contract's real content: a new
/// service that reacts to a session end without implementing it fails the gate.
/// </para>
///
/// <para>
/// A service whose reset its owner drives declares this contract without subscribing
/// it (the item arbitration table, the id coordinator, the item snapshot service and
/// the world-state message service are the current examples); a service that reacts to
/// the session end edge subscribes this method itself. The Application layer's kernel
/// protocol service follows the same method name and subscription shape under a
/// declared gate exemption, because Application may reference GameState and Protocol
/// only and so cannot implement this Runtime contract without breaking the
/// project-direction gate.
/// </para>
///
/// <para>
/// A reset is a pure drop of the dead session's facts — it never reaches forward
/// into the next session, and a service that owns no session state must not
/// implement the interface merely to look uniform.
/// </para>
/// </summary>
public interface ISessionReset
{
	/// <summary>Drops every fact this service holds for the session that just ended.</summary>
	void ResetSessionState();
}
