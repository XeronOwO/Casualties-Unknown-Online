// eval declaration: identity-mismatch
// declares: AcceptanceIdentityMismatch
// serves: handshake-identity-and-refusal-report (a peer declaring one wrong identity dimension is refused by name, and the guest renders the refusal line).
//
// Sends the exact guest→host Handshake message a peer whose identity block differs sends, over the live session's own PacketSender.
// `dimension` is game | cuo | save; `value` is the crafted value for that dimension, and the other two carry this client's own
// values, so the host's comparison fails on exactly the named dimension. The message is built and sent by reflection: nothing
// here references a CUO type at compile time, because a compile error inside a declaration poisons that client's whole evaluator.
using System;
using System.Reflection;

public static class AcceptanceIdentityMismatch
{
	const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

	public static string Send(string dimension, string value)
	{
		var runtime = FindAssembly("CasualtiesUnknownOnline.Runtime");
		if (runtime == null) { return "no-runtime-assembly"; }
		var bootstrap = runtime.GetType("CasualtiesUnknownOnline.Runtime.CuoBootstrap", false);
		if (bootstrap == null) { return "no-bootstrap"; }
		var servicesProperty = bootstrap.GetProperty("Services", BindingFlags.Public | BindingFlags.Static);
		var services = servicesProperty == null ? null : servicesProperty.GetValue(null, null);
		if (services == null) { return "no-services"; }
		var sessionType = runtime.GetType("CasualtiesUnknownOnline.Runtime.Session.SessionService", false);
		var identityType = runtime.GetType("CasualtiesUnknownOnline.Runtime.Session.ILocalPeerIdentity", false);
		if (sessionType == null || identityType == null) { return "no-session-type"; }
		var getService = services.GetType().GetMethod("GetService", new[] { typeof(Type) });
		if (getService == null) { return "no-get-service"; }
		var session = getService.Invoke(services, new object[] { sessionType });
		var identity = getService.Invoke(services, new object[] { identityType });
		if (session == null) { return "no-session"; }
		if (identity == null) { return "no-identity"; }
		var hostProperty = sessionType.GetProperty("HostSteamId", BindingFlags.Public | BindingFlags.Instance);
		var host = hostProperty == null ? 0UL : (ulong)hostProperty.GetValue(session, null);
		if (host == 0UL) { return "no-host"; }
		var senderField = sessionType.GetField("_sender", BindingFlags.NonPublic | BindingFlags.Instance);
		var sender = senderField == null ? null : senderField.GetValue(session);
		if (sender == null) { return "no-sender"; }
		var netMsgType = runtime.GetType("CasualtiesUnknownOnline.Runtime.Protocol.NetMsg", false);
		var handshakeType = runtime.GetType("CasualtiesUnknownOnline.Runtime.Protocol.Messages.HandshakeMsg", false);
		var sceneType = runtime.GetType("CasualtiesUnknownOnline.Runtime.Protocol.Messages.SceneStateMsg", false);
		if (netMsgType == null || handshakeType == null || sceneType == null) { return "no-message-types"; }

		var game = (string)identityType.GetProperty("GameBuild").GetValue(identity, null);
		var cuo = (string)identityType.GetProperty("CuoBuild").GetValue(identity, null);
		var schema = (int)identityType.GetProperty("SaveSchema").GetValue(identity, null);
		if (dimension == "game") { game = value; }
		else if (dimension == "cuo") { cuo = value; }
		else if (dimension == "save") { schema = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture); }
		else { return "unknown-dimension:" + dimension; }

		var kind = Enum.Parse(netMsgType, "Handshake");
		var message = Activator.CreateInstance(handshakeType);
		handshakeType.GetProperty("Protocol", Any).SetValue(message, 1, null);
		handshakeType.GetProperty("Scene", Any).SetValue(message, Activator.CreateInstance(sceneType), null);
		var modsProperty = handshakeType.GetProperty("Mods", Any);
		if (modsProperty != null)
		{
			var emptyMods = Activator.CreateInstance(modsProperty.PropertyType);
			if (emptyMods != null) { modsProperty.SetValue(message, emptyMods, null); }
		}
		handshakeType.GetProperty("GameBuild", Any).SetValue(message, game, null);
		handshakeType.GetProperty("CuoBuild", Any).SetValue(message, cuo, null);
		handshakeType.GetProperty("SaveSchema", Any).SetValue(message, schema, null);
		var trySend = senderField.FieldType.GetMethod("TrySend", new[] { typeof(ulong), netMsgType, typeof(object), typeof(bool) });
		if (trySend == null) { return "no-try-send"; }
		var sent = (bool)trySend.Invoke(sender, new object[] { host, kind, message, true });
		return "sent:" + dimension + ":" + value + ":" + (sent ? "true" : "false") + ":host=" + host.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
