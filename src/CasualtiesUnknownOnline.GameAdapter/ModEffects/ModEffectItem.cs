using CasualtiesUnknownOnline.Abstractions;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.ModEffects;

/// <summary>
/// The named item operations, implemented against the live game item the native
/// call handed over. Like <see cref="ModEffectBody"/> and
/// <see cref="ModEffectLimb"/> beside it, this is a mapping and nothing else.
///
/// <para>
/// The two writes differ on purpose, and the difference is the game's own:
/// <see cref="AddCondition"/> is the plain field arithmetic the game's shipped items
/// perform (<c>item.condition -= 0.34f</c>), while <see cref="SetCondition"/> goes
/// through <c>Item.SetCondition</c>, which scales a liquid container's contents to
/// match. An action that spends durability wants the first; one that pins a value
/// wants the second, exactly as the game's own code chooses between them.
/// </para>
/// </summary>
internal sealed class ModEffectItem(Item item) : IModEffectItem
{
	private readonly Item _item = item;

	/// <inheritdoc />
	public float Condition => _item.condition;

	/// <inheritdoc />
	public void AddCondition(float amount) => _item.condition += amount;

	/// <inheritdoc />
	public void SetCondition(float value) => _item.SetCondition(value);

	/// <inheritdoc />
	public GameObject? NativeObject => _item == null ? null : _item.gameObject; // Unity object — == (destroyed-object check)
}
