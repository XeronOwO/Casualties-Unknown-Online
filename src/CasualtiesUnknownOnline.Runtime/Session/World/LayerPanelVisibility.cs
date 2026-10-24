namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Whether the HOST's end-of-layer panel should be on screen, decided from the share of LIVING players that has
/// finished the layer (the user's 2026-10-24 ruling). The native panel's own trigger is the LOCAL body's position,
/// which cannot answer the question when the host's body is dead or when the host is not the one at the bottom, so
/// the host decides from the group's state instead and this rule is what it decides with.
///
/// <para>
/// Pure on purpose: three counts decide it, so the "every living player" default, a majority threshold and the
/// degenerate cases (nobody alive, nobody finished) are cases rather than surprises.
/// </para>
/// </summary>
public static class LayerPanelVisibility
{
	/// <summary>
	/// True when at least one living body exists and the finished share has reached
	/// <paramref name="requiredShare"/> (0..1; 1 = every living body). A share of 0 shows the panel as soon as
	/// anybody is alive, which is the "no threshold" setting.
	/// </summary>
	public static bool ShouldShow(int livingBodies, int bodiesAtTheFloor, float requiredShare)
	{
		if (livingBodies <= 0)
		{
			return false;
		}

		if (requiredShare <= 0f)
		{
			return true;
		}

		return bodiesAtTheFloor / (float)livingBodies >= requiredShare;
	}
}
