namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One parsed write: the item it names and the property it asks for, with the
/// value in the shape that property's own kind uses. Which field of this record
/// carries the value is decided by <see cref="Property"/>:
/// <list type="bullet">
/// <item><see cref="ItemDataProperty.Condition"/> — <see cref="Number"/>;</item>
/// <item><see cref="ItemDataProperty.Favourited"/> — <see cref="Flag"/>;</item>
/// <item><see cref="ItemDataProperty.Liquid"/> — <see cref="Name"/> (the liquid id)
/// and <see cref="Number"/> (the millilitres);</item>
/// <item><see cref="ItemDataProperty.Component"/> — <see cref="Component"/> (the
/// component's type name), <see cref="Name"/> (the field's name) and
/// <see cref="Text"/> (the value as written).</item>
/// </list>
///
/// <para>
/// A component field's value stays TEXT until execution on purpose: which kind it
/// has is a fact about the target's own state, so it is read from the capture the
/// command takes — the same way the read half renders a field by the kind its row
/// carries — and a value that does not parse as that kind is refused there, by
/// name.
/// </para>
/// </summary>
internal sealed record ItemDataWrite(
	ItemDataQuery Target,
	ItemDataProperty Property,
	float Number,
	bool Flag,
	string Name,
	string Component,
	string Text);
