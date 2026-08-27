namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>One liquid stack inside a WaterContainerItem.</summary>
public readonly record struct ItemLiquidStack(string LiquidId, float Amount);
