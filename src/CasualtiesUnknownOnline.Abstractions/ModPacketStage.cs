namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The position of one <see cref="ModPacketHandler"/> in its packet's chain.
/// A stage is a PHASE, and the framework runs the phases in the fixed order
/// below whatever order the handlers were declared in; inside one phase the
/// declared order is kept. The two boundaries a mod can order against are the
/// framework's own steps: <see cref="Validate"/> runs before the framework
/// relays a report to the other members, and <see cref="Apply"/> runs after
/// that decision — so a refusal during validation stops both the application
/// and the relay.
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public enum ModPacketStage
{
	/// <summary>
	/// Runs before the packet is applied or relayed. This is the only stage
	/// that may refuse a delivery (<see cref="IModPacketContext.Refuse"/>);
	/// the first refusal ends the whole delivery — no later stage runs and the
	/// host relays nothing.
	/// </summary>
	Validate = 0,

	/// <summary>Runs the packet's own effect, in declaration order.</summary>
	Apply = 1,

	/// <summary>
	/// Runs after the packet's effect, in declaration order — presentation and
	/// bookkeeping that must not be able to refuse a delivery. A throw here is
	/// isolated exactly like a throw in any other stage.
	/// </summary>
	Observe = 2,
}
