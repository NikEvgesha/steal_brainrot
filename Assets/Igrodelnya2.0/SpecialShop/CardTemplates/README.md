# SpecialShop: four active card templates

Only these four prefabs are instantiated by `SpecialShop`:

1. `Active/SpecialShopCard_SmallUse.prefab` - small card with **Use**.
2. `Active/SpecialShopCard_Small.prefab` - small card without **Use**.
3. `Active/SpecialShopCard_Wide.prefab` - full-width card.
4. `Active/SpecialShopCard_EternalPack.prefab` - dedicated Eternal Pack card.

Selection is data-driven:

- Eternal Pack always uses template 4.
- `Items Per Row = 1` uses template 3.
- A half-width consumable uses template 1.
- Every other half-width product uses template 2.

The other style-specific prefabs are retained only as an archive so previous
manual layout work is not lost. They are not referenced at runtime.

## PriceVal

`PriceVal` is an optional TMP label for the platform/real-money price. Keep its
GameObject disabled in the prefab. At runtime `SpecialShopSlot` finds it by
name, enables it only for a real-money price, and hides the regular `Price`
label. If a template has no `PriceVal`, the regular label remains the fallback.

Use `Tools > Special Shop > Assign Four Active Card Templates` to restore the
four references on `SpecialShop.prefab` without changing the card layouts.

## Eternal reward cell

`Active/SpecialShopEternalRewardCell.prefab` is the visual template for one
reward in the Eternal Pack. Edit `Icon`, `Amount`, `Price`,
`PriceCurrencyIcon`, and `Next` in Prefab Mode. At runtime the track clones
this layout and inserts the current reward values and icons into it.
