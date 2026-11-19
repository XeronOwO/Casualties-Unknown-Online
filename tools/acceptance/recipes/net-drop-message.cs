// recipe: net-drop-message
// args: mode=s msg=s seconds=n
// serves: layer-change-member-recovery rows 1-4 — the world-entry instruction lost while every other frame arrives
// returns: ok, mode, msg, armed, dropped, remainingMs, subscribers, error, detail
//
// Loses exactly ONE message family on the LOCAL client for a window: it parks the receive seam
// (PacketReceiver.MessageArrived — one step downstream of the router's MessageReceived event that
// net-receive-blackout parks as a whole) and forwards every frame except the named one. The rest of
// the session keeps running, so the shape is the one-shot instruction the lazy-P2P swallow costs while
// the periodic and absolute traffic still lands — which is what a recovery for a lost world-entry
// invitation has to be driven against, without the blunt blackout's 15-second ceiling and without
// losing the member's own exit story.
//
// The nested delegate reads its state back out of the AppDomain instead of capturing the outer lambda's
// locals: the evaluator's Mono.CSharp cannot compile a nested delegate that closes over them. One
// invocation is one fresh evaluation, so arm, status and off are three calls; the window also expires on
// its own at `seconds`, so an off that arrives late cannot extend it.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var name = {{s:msg}};
	var seconds = (int){{n:seconds}};
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var receiver = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.PacketReceiver)) as CasualtiesUnknownOnline.Runtime.Session.PacketReceiver;
	if (receiver == null) { return "{\"ok\":false,\"error\":\"no-receiver\"}"; }
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const string key = "cuo.acceptance.net-drop-message";
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var state = System.AppDomain.CurrentDomain.GetData(key) as object[];
	if (mode == "status") {
		if (state == null) { return "{\"ok\":true,\"mode\":\"status\",\"armed\":false,\"dropped\":0,\"remainingMs\":0}"; }
		var running = state[3] as int[];
		var left = (int)state[2] - System.Environment.TickCount;
		var droppedSoFar = running == null ? 0 : running[0];
		return "{\"ok\":true,\"mode\":\"status\",\"armed\":true,\"dropped\":" + droppedSoFar.ToString(inv)
			+ ",\"remainingMs\":" + (left > 0 ? left : 0).ToString(inv) + "}";
	}
	if (mode == "arm") {
		if (state != null) { return "{\"ok\":false,\"error\":\"already-armed\"}"; }
		if (!System.Enum.IsDefined(typeof(CasualtiesUnknownOnline.Runtime.Protocol.NetMsg), name)) {
			return "{\"ok\":false,\"error\":\"unknown-message\",\"detail\":\"not a NetMsg member\"}";
		}
		if (seconds < 1 || seconds > 120) { return "{\"ok\":false,\"error\":\"bad-seconds\",\"detail\":\"seconds must be 1..120\"}"; }
		var wanted = (byte)(CasualtiesUnknownOnline.Runtime.Protocol.NetMsg)System.Enum.Parse(typeof(CasualtiesUnknownOnline.Runtime.Protocol.NetMsg), name);
		var field = receiver.GetType().GetField("MessageArrived", flags);
		if (field == null) { return "{\"ok\":false,\"error\":\"no-event-field\"}"; }
		var current = field.GetValue(receiver) as System.Action<ulong, byte[]>;
		if (current == null) { return "{\"ok\":false,\"error\":\"already-parked\"}"; }
		var stateNow = new object[] { current, wanted, System.Environment.TickCount + seconds * 1000, new int[1] };
		System.AppDomain.CurrentDomain.SetData(key, stateNow);
		var filter = new System.Action<ulong, byte[]>(delegate(ulong sender, byte[] frame) {
			var armed = System.AppDomain.CurrentDomain.GetData("cuo.acceptance.net-drop-message") as object[];
			if (armed == null) { return; }
			var forward = armed[0] as System.Action<ulong, byte[]>;
			if (forward == null) { return; }
			if (frame != null && frame.Length > 0 && frame[0] == (byte)armed[1] && System.Environment.TickCount < (int)armed[2]) {
				var count = armed[3] as int[];
				if (count != null) { count[0] = count[0] + 1; }
				return;
			}
			forward(sender, frame);
		});
		field.SetValue(receiver, filter);
		var subscribers = current.GetInvocationList().Length;
		return "{\"ok\":true,\"mode\":\"arm\",\"msg\":\"" + name + "\",\"armed\":true,\"dropped\":0,\"remainingMs\":"
			+ (seconds * 1000).ToString(inv) + ",\"subscribers\":" + subscribers.ToString(inv) + "}";
	}
	if (mode == "off") {
		if (state == null) { return "{\"ok\":false,\"error\":\"not-armed\"}"; }
		var field = receiver.GetType().GetField("MessageArrived", flags);
		if (field != null) { field.SetValue(receiver, state[0]); }
		System.AppDomain.CurrentDomain.SetData(key, null);
		var running = state[3] as int[];
		var dropped = running == null ? 0 : running[0];
		return "{\"ok\":true,\"mode\":\"off\",\"msg\":\"" + name + "\",\"armed\":false,\"dropped\":" + dropped.ToString(inv) + "}";
	}
	return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be arm, status or off\"}";
}))()
