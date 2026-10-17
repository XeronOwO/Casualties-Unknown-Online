namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>A boolean. <c>true</c>/<c>false</c> in the text; <c>1b</c>/<c>0b</c> is accepted as the same truth value.</summary>
public sealed record SnbtBool(bool Value) : SnbtValue
{
	public override string Kind => "a yes/no value";
}
