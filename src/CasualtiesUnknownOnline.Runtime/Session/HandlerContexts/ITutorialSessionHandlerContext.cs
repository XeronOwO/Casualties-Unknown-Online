using CasualtiesUnknownOnline.Runtime.Session.Tutorial;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The session + tutorial-claw control surface a tutorial-claw packet handler may use.</summary>
public interface ITutorialSessionHandlerContext
{
	ISessionControl Session { get; }
	ITutorialClawControl TutorialClaw { get; }
}
