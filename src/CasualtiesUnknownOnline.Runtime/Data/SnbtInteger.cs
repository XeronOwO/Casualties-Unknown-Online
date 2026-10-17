namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>
/// An integer. The wire's int fields and the component model's int/enum fields bind from this
/// value; its <see cref="long"/> range is the parser's, not the item's — the adapter refuses a
/// value its field cannot take.
/// </summary>
public sealed record SnbtInteger(long Value) : SnbtValue
{
	public override string Kind => "an integer";
}
