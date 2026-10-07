using System;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The capture window a cross-player limb action needs around the item's OWN
/// native <c>useLimbAction</c> call: the medical capture scope (so a clip the
/// native delegate plays is classified and relayed instead of staying the
/// operator's alone) plus the display-body window (the displayed copy is parked
/// off-world by <c>RemoteMedicalCoordinator</c>, so a 3D clip would otherwise be
/// reported at (0, -10000)).
/// <para>
/// The two close in the order the gore patches established: the capture first,
/// so nothing the parked-position restore does can be reported. Entered around
/// the ONE synchronous native call, never kept open across frames — the
/// minigame the delegate may start runs its own frames outside this window.
/// </para>
/// </summary>
internal static class NativeLimbActionScope
{
	internal static IDisposable Enter(Body? body) =>
		new Scope(CallContext.Enter(CallContext.Origin.CharacterMedicalUse), RemoteMedicalDisplayCapture.Enter(body));

	private sealed class Scope(IDisposable scope, IDisposable window) : IDisposable
	{
		private readonly IDisposable _scope = scope;
		private readonly IDisposable _window = window;

		public void Dispose()
		{
			_scope.Dispose();
			_window.Dispose();
		}
	}
}
