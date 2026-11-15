using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>The drill pod gate's verdict, with the living count and the members still missing from the pod.</summary>
public readonly record struct PodGateResult(bool CanFire, int Living, int AtPod, IReadOnlyList<string> Missing);
