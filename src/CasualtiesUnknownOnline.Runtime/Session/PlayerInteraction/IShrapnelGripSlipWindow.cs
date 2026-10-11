using System;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The engine random stream one shrapnel grip slip runs in, held open around a native call.
/// <see cref="IDisposable.Dispose"/> puts the engine stream back where it was, so the window never
/// outlives the synchronous call it wraps.
/// </summary>
public interface IShrapnelGripSlipWindow : IDisposable
{
}
