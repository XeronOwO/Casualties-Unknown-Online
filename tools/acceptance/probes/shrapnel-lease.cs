// recipe: shrapnel-lease
// args: mode=s piece=n
// serves: remote-medical-stage-2-shrapnel-multiplayer row 2 (two operators grabbing the same piece)
// returns: ok, mode, piece, result — "sent", "no-active-session", a seam name, or "threw:..."
//
// One ownership report per call, with no break and no position, so a second client can contend for the same piece.
// Run after `remote-wound-special mode=special` on both operators: the local client must own a shrapnel session
// (the sender delegate is bound then), which `mode=status` reports before the grab attempt.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var piece = (int){{n:piece}};
	var outcome = "bad-mode";
	if (mode == "status") { outcome = AcceptanceShrapnelLease.Status(); }
	else if (mode == "grab") { outcome = AcceptanceShrapnelLease.Report(piece, true, false, true); }
	else if (mode == "hold") { outcome = AcceptanceShrapnelLease.Report(piece, true, false, false); }
	else if (mode == "release") { outcome = AcceptanceShrapnelLease.Report(piece, false, true, true); }
	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"piece\":" + piece + ",\"result\":\"" + outcome + "\"}";
}))()
