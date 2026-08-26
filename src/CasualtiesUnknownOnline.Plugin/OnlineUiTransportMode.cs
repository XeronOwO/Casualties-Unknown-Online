namespace CasualtiesUnknownOnline;

/// <summary>
/// The mutually-exclusive connection transport selected on the Online UI Home
/// page. This is presentation-only state: the actual network router stays on
/// Steam until an IP-direct host/join action switches it.
/// </summary>
internal enum OnlineUiTransportMode
{
	Steam,
	IpDirect,
}
