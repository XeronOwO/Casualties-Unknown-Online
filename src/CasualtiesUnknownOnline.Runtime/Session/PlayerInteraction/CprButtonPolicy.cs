namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The CPR control's state machine (ticket `remote-medical-cpr.md`, stage 1c). It is pure so the three states are cases:
/// the operator's own session wins over the start (a second start would be refused by the host anyway), and the
/// patient's arrest is what makes the control appear at all.
/// </summary>
public static class CprButtonPolicy
{
	/// <summary>
	/// <paramref name="hasRemoteFocus"/> — a remote body is open in the wound view;
	/// <paramref name="patientInArrest"/> — that body's heart has stopped (`Body.inCardiacArrest`, `heartRate &lt; 20`);
	/// <paramref name="ownCprRunning"/> — this client already runs a CPR operation on that body.
	/// </summary>
	public static CprButtonState Decide(bool hasRemoteFocus, bool patientInArrest, bool ownCprRunning)
	{
		if (!hasRemoteFocus)
		{
			return CprButtonState.Hidden;
		}

		if (ownCprRunning)
		{
			return CprButtonState.Compress;
		}

		return patientInArrest ? CprButtonState.Start : CprButtonState.Hidden;
	}

	/// <summary>The compression interval to send for two press times, never zero (the update refuses a non-positive interval).</summary>
	public static float IntervalSeconds(float previousPressSeconds, float nowSeconds)
	{
		var interval = nowSeconds - previousPressSeconds;
		return interval <= 0f ? 0.01f : interval;
	}
}
