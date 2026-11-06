// eval declaration: version-mismatch
// declares: AcceptanceVersionMismatch
// serves: remote-medical-stage-1-injection-session row 20 (a peer declaring a distant protocol is refused, not shimmed).
//
// Sends the exact guest→host Handshake message an old client would send, with an arbitrary protocol number, over the live
// session's own PacketSender. The host compares the number in HandshakeHandler and returns before touching any state, so the
// only trace is its own log line naming the peer's protocol and ours. Reflection-only on purpose: a compile error inside a
// declaration poisons that client's whole evaluator, so nothing here references a CUO type at compile time.
using System;
using System.Reflection;

public static class AcceptanceVersionMismatch
{
	const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

	public static string Send(int protocol)
	{
		var runtime = FindAssembly("CasualtiesUnknownOnline.Runtime");
		if (runtime == null) { return "no-runtime-assembly"; }
		var bootstrap = runtime.GetType("CasualtiesUnknownOnline.Runtime.CuoBootstrap", false);
		if (bootstrap == null) { return "no-bootstrap"; }
		var servicesProperty = bootstrap.GetProperty("Services", BindingFlags.Public | BindingFlags.Static);
		var services = servicesProperty == null ? null : servicesProperty.GetValue(null, null);
		if (services == null) { return "no-services"; }
		var sessionType = runtime.GetType("CasualtiesUnknownOnline.Runtime.Session.SessionService", false);
		if (sessionType == null) { return "no-session-type"; }
		var getService = services.GetType().GetMethod("GetService", new[] { typeof(Type) });
		if (getService == null) { return "no-get-service"; }
		var session = getService.Invoke(services, new object[] { sessionType });
		if (session == null) { return "no-session"; }
		var hostProperty = sessionType.GetProperty("HostSteamId", BindingFlags.Public | BindingFlags.Instance);
		var host = hostProperty == null ? 0UL : (ulong)hostProperty.GetValue(session, null);
		if (host == 0UL) { return "no-host"; }
		var senderField = sessionType.GetField("_sender", BindingFlags.NonPublic | BindingFlags.Instance);
		if (senderField == null) { return "no-sender-field"; }
		var sender = senderField.GetValue(session);
		if (sender == null) { return "no-sender"; }
		var netMsgType = runtime.GetType("CasualtiesUnknownOnline.Runtime.Protocol.NetMsg", false);
		var handshakeType = runtime.GetType("CasualtiesUnknownOnline.Runtime.Protocol.Messages.HandshakeMsg", false);
		var sceneType = runtime.GetType("CasualtiesUnknownOnline.Runtime.Protocol.Messages.SceneStateMsg", false);
		if (netMsgType == null || handshakeType == null || sceneType == null) { return "no-message-types"; }
		var kind = Enum.Parse(netMsgType, "Handshake");
		var message = Activator.CreateInstance(handshakeType);
		handshakeType.GetProperty("Protocol", Any).SetValue(message, protocol, null);
		handshakeType.GetProperty("Scene", Any).SetValue(message, Activator.CreateInstance(sceneType), null);
		var trySend = senderField.FieldType.GetMethod("TrySend", new[] { typeof(ulong), netMsgType, typeof(object), typeof(bool) });
		if (trySend == null) { return "no-try-send"; }
		var sent = (bool)trySend.Invoke(sender, new object[] { host, kind, message, true });
		return "sent:" + protocol.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + (sent ? "true" : "false") + ":host=" + host.ToString(System.Globalization.CultureInfo.InvariantCulture);
	}

	static Assembly FindAssembly(string name)
	{
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++)
		{
			try
			{
				if (assemblies[i].GetName().Name == name) { return assemblies[i]; }
			}
			catch
			{
			}
		}
		return null;
	}
}
