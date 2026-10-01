namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// The Steam Datagram Relay reading the send-failure diagnostics append:
/// whether the relay status is Current (the gate the inline diagnostics used
/// before printing the line) plus the two availability readings and Steam's
/// debug message. The availability readings are carried as strings because
/// their only consumer is a log line.
/// </summary>
internal readonly record struct SteamRelayStatus(
	bool Current,
	string AnyRelay,
	string NetworkConfig,
	string Debug);
