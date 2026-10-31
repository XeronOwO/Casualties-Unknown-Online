// eval declaration: audio-probe
// declares: AcceptanceAudioProbe
// serves: a compile probe for the audio-watch declaration (kept tiny on purpose)
public static class AcceptanceAudioProbe
{
	public static string Hits = "";

	public static string Start()
	{
		Hits = "started";
		return "started";
	}
}
