namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Pure host/guest decision for the post-session menu return. L0-testable so
/// the save-authority rule cannot be re-derived differently in the adapter.
/// </summary>
public static class RunMenuReturnPolicy
{
	public static RunMenuReturnMode Decide(SessionRole role, bool inWorld)
	{
		if (!inWorld)
		{
			return RunMenuReturnMode.None;
		}

		return role == SessionRole.Host ? RunMenuReturnMode.SaveAndMenu : RunMenuReturnMode.MenuOnly;
	}
}
