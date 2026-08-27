namespace CasualtiesUnknownOnline.GameState.Kernel;

/// <summary>
/// Internal domain module contract. The kernel routes typed commands and
/// reduces typed events; domain code never sees another domain's internals.
/// </summary>
internal interface IDomainModule
{
	bool CanHandle(GameCommand command);

	bool CanReduce(GameEvent @event);

	DomainDecision Decide(GameCommand command, KernelReadModel state, CommandContext context);

	void Reduce(GameEvent @event, MutableKernelState state);

	void AssertInvariants(KernelReadModel state);
}
