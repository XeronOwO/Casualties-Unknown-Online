namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>A string. The writer quotes it and escapes what the parser escapes, so a read value can be written back.</summary>
public sealed record SnbtString(string Value) : SnbtValue
{
	public override string Kind => "a string";
}
