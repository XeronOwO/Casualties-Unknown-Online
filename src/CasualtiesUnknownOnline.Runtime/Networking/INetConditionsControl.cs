namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// The diagnostics control a test harness writes: the profile in force on the local transport. One
/// implementation exists (the IP-direct transport); the Steam transport stays out of scope because the
/// acceptance pair runs IP-direct.
/// </summary>
public interface INetConditionsControl
{
	NetConditionsProfile Current { get; }

	void Set(NetConditionsProfile profile);

	void Clear();
}
