// recipe: wire-generation-probe
// args: mode=s kind=s
// serves: world-layer-generation-identity row 7, generation-identity-remaining-families row 6
// returns: ok, mode, kind, armed, captured, decoded, dropped, frames, error, detail
//
// Captures the REAL frames of one family off this client's own receive seam (PacketReceiver.MessageArrived,
// the same transport path the blackout parks) and decodes each frame's Generation stamp with the product's
// own codec (NetPacket.DecodePayload over the registry's payload type). This is the frame-level proof that
// the stamp is on the wire and not added by the caller: the decoded stamp is read from bytes this process
// received from its peer. mode=arm parks the handler in this process's AppDomain data; mode=read decodes
// what arrived so far; mode=off unsubscribes and clears. One invocation is one fresh evaluation, so arm,
// read and off are three calls. Read-only with respect to the world; the capture list is what it is.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var receiver = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.PacketReceiver)) as CasualtiesUnknownOnline.Runtime.Session.PacketReceiver;
	if (receiver == null) { return "{\"ok\":false,\"error\":\"no-receiver\"}"; }
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const string key = "cuo.acceptance.wire-generation-probe";
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var mode = {{s:mode}};
	var kind = {{s:kind}};
	var state = System.AppDomain.CurrentDomain.GetData(key) as object[];
	if (mode == "arm") {
		if (state != null) { return "{\"ok\":false,\"error\":\"already-armed\"}"; }
		var wanted = new System.Collections.Generic.List<byte>();
		if (kind == "block") { wanted.Add((byte)40); wanted.Add((byte)42); wanted.Add((byte)89); wanted.Add((byte)136); }
		else if (kind == "entity") { wanted.Add((byte)68); wanted.Add((byte)134); wanted.Add((byte)135); }
		else if (kind == "trap") { wanted.Add((byte)79); }
		else if (kind == "all") { for (var i = 1; i <= 141; i++) { wanted.Add((byte)i); } }
		else { return "{\"ok\":false,\"error\":\"bad-kind\",\"detail\":\"kind must be block, entity, trap or all\"}"; }
		var list = new System.Collections.Generic.List<object[]>();
		var gate = new object();
		var dropped = new int[1];
		var handler = new System.Action<ulong, byte[]>(delegate(ulong sender, byte[] frame) {
			if (frame == null || frame.Length < 1) { return; }
			var wantedHere = false;
			for (var i = 0; i < wanted.Count; i++) { if (wanted[i] == frame[0]) { wantedHere = true; break; } }
			if (!wantedHere) { return; }
			var copy = new byte[frame.Length];
			System.Array.Copy(frame, copy, frame.Length);
			var entry = new object[4];
			entry[0] = sender;
			entry[1] = frame[0];
			entry[2] = copy;
			entry[3] = System.DateTime.UtcNow.Ticks;
			lock (gate) {
				if (list.Count >= 256) { list.RemoveAt(0); dropped[0] = dropped[0] + 1; }
				list.Add(entry);
			}
		});
		receiver.MessageArrived += handler;
		System.AppDomain.CurrentDomain.SetData(key, new object[] { list, gate, handler, wanted, dropped });
		return "{\"ok\":true,\"mode\":\"arm\",\"kind\":\"" + kind + "\",\"armed\":true,\"captured\":0}";
	}
	if (mode == "read") {
		if (state == null) { return "{\"ok\":false,\"error\":\"not-armed\"}"; }
		var list = state[0] as System.Collections.Generic.List<object[]>;
		object[][] snapshot;
		lock (state[1]) { snapshot = list.ToArray(); }
		var captured = snapshot.Length;
		var decoded = 0;
		var sb = new System.Text.StringBuilder();
		var decodeMethod = typeof(CasualtiesUnknownOnline.Runtime.Protocol.NetPacket).GetMethod("DecodePayload", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

		for (var i = 0; i < snapshot.Length && decoded < 48; i++) {
			var entry = snapshot[i];
			var msgId = (CasualtiesUnknownOnline.Runtime.Protocol.NetMsg)(byte)entry[1];
			var sender = System.Convert.ToUInt64(entry[0], inv);
			var frame = (byte[])entry[2];
			CasualtiesUnknownOnline.Runtime.Session.NetMessageMetadata metadata;
			var known = CasualtiesUnknownOnline.Runtime.Session.NetMessageRegistry.TryGet(msgId, out metadata);
			var hasStamp = false;
			var run = 0UL;
			var layer = -1;
			if (known && decodeMethod != null) {
				object payload = null;
				try { payload = decodeMethod.MakeGenericMethod(metadata.PayloadType).Invoke(null, new object[] { frame }); }
				catch (System.Exception decodeFailure) { payload = null; }
				if (payload != null) {
					var generationProperty = metadata.PayloadType.GetProperty("Generation", flags);
					var generation = generationProperty == null ? null : generationProperty.GetValue(payload, null);
					if (generation != null) {
						var epochProperty = generation.GetType().GetProperty("RunEpoch", flags);
						var layerProperty = generation.GetType().GetProperty("LayerIndex", flags);
						if (epochProperty != null) { run = System.Convert.ToUInt64(epochProperty.GetValue(generation, null), inv); hasStamp = true; }
						if (layerProperty != null) { layer = System.Convert.ToInt32(layerProperty.GetValue(generation, null), inv); }
					}
				}
			}
			if (decoded > 0) { sb.Append(','); }
			decoded = decoded + 1;
			sb.Append("{\"msg\":\"").Append(msgId.ToString()).Append("\",\"from\":\"").Append(sender.ToString(inv)).Append('"')
				.Append(",\"bytes\":").Append(frame.Length.ToString(inv))
				.Append(",\"stamp\":").Append(hasStamp ? "true" : "false")
				.Append(",\"run\":").Append(hasStamp ? run.ToString(inv) : "null")
				.Append(",\"layer\":").Append(hasStamp ? layer.ToString(inv) : "null").Append('}');
		}
		return "{\"ok\":true,\"mode\":\"read\",\"kind\":\"" + kind + "\",\"armed\":true"
			+ ",\"captured\":" + captured.ToString(inv)
			+ ",\"decoded\":" + decoded.ToString(inv)
			+ ",\"dropped\":" + ((int[])state[4])[0].ToString(inv)
			+ ",\"frames\":[" + sb.ToString() + "]}";
	}
	if (mode == "off") {
		if (state == null) { return "{\"ok\":false,\"error\":\"not-armed\"}"; }
		receiver.MessageArrived -= (System.Action<ulong, byte[]>)state[2];
		System.AppDomain.CurrentDomain.SetData(key, null);
		return "{\"ok\":true,\"mode\":\"off\",\"armed\":false,\"captured\":0}";
	}
	return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be arm, read or off\"}";
}))()
