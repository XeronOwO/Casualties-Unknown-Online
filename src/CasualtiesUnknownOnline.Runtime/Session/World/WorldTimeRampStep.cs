namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>One ramp step: the <c>Time.timeScale</c> to write, and whether the ramp reached its target.</summary>
public readonly record struct WorldTimeRampStep(float TimeScale, bool Done);
