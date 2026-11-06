// eval declaration: shrapnel-lease
// declares: AcceptanceShrapnelLease
// serves: remote-medical-stage-2-shrapnel-multiplayer row 2 (two operators grabbing the same piece).
//
// Why this exists: `grip-slip-drive mode=break` sends a grab report and breaks the piece inside the same call, so the
// piece's owner is released again before a second client can contend for it. Contention needs one ownership report and
// nothing else, which is what this declaration does. The host keeps one owner per piece
// (`ShrapnelOperationSessionService`, `piece.Owner`) and rejects every non-owner move, so a second client's grab either
// takes the piece (owner was empty) or is refused with the session's own log line.
using System;
using System.Reflection;

public static class AcceptanceShrapnelLease
{
	const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

	/// <summary>Sends one ownership report for a piece: grab, release, or a plain hold without an ownership change.</summary>
	public static string Report(int piece, bool grabbed, bool released, bool ownership)
	{
		try
		{
			var updateType = Find("CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.ShrapnelPieceUpdate");
			if (updateType == null) { return "no-update-type"; }
			var handlerType = Find("CasualtiesUnknownOnline.GameAdapter.RemoteMedicalOperationHandler");
			if (handlerType == null) { return "no-handler-type"; }
			var report = handlerType.GetMethod("ReportShrapnelUpdate", Flags);
			if (report == null) { return "no-report-method"; }
			var msg = Activator.CreateInstance(updateType);
			updateType.GetProperty("PieceIndex").SetValue(msg, piece, null);
			updateType.GetProperty("Grabbed").SetValue(msg, grabbed, null);
			updateType.GetProperty("Released").SetValue(msg, released, null);
			updateType.GetProperty("OwnershipChange").SetValue(msg, ownership, null);
			report.Invoke(null, new object[] { msg });
			return "sent";
		}
		catch (Exception ex)
		{
			return "threw:" + ex.GetType().Name + ":" + ex.Message;
		}
	}

	/// <summary>The local client's own shrapnel session, so a silent no-op names its cause.</summary>
	public static string Status()
	{
		try
		{
			var handlerType = Find("CasualtiesUnknownOnline.GameAdapter.RemoteMedicalOperationHandler");
			if (handlerType == null) { return "no-handler-type"; }
			var activeField = handlerType.GetField("_activeShrapnel", Flags);
			var senderField = handlerType.GetField("_shrapnelUpdateSender", Flags);
			var active = activeField != null ? activeField.GetValue(null) : null;
			var sender = senderField != null ? senderField.GetValue(null) : null;
			if (active == null) { return "no-active-session"; }
			var opId = active.GetType().GetProperty("OperationId", Flags);
			var id = opId != null ? opId.GetValue(active, null) : null;
			return "session:" + (id != null ? id.ToString() : "?") + ":sender=" + (sender != null ? "present" : "missing");
		}
		catch (Exception ex)
		{
			return "threw:" + ex.GetType().Name;
		}
	}

	static Type Find(string fullName)
	{
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++)
		{
			try
			{
				var type = assemblies[i].GetType(fullName, false);
				if (type != null) { return type; }
			}
			catch
			{
			}
		}
		return null;
	}
}
