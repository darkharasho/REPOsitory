# Changelog

## 1.4.0
- **Reverted all held-weapon height/stabilization fixes.** None of the approaches (SolidAim-port stabilization, proportional grab-drop restore, blocking ScalerCore's `ForceGrabPointVerticalScalePatch` lift) reliably kept guns/melee at hand height for shrunk players, so they now use ScalerCore's default hold. Removed `HeldGunStabilizationPatch`, `WeaponGrabVerticalRestorePatch`, `ForceGrabPointWeaponBlocker`, and the `Jangnana.SolidAim` soft-dependency. The whip-knockdown and cart-recoil fixes are kept.
- **New: `ShrinkValuables` toggle** (default on). Master on/off for shrinking valuables. When off, valuables spawn full-size — but the separate in-cart shrink (`CartScale`) still applies, so a valuable shrinks once placed in the cart.
- **New: `ShrinkCart` toggle** (default **on**). The cart (C.A.R.T) is an equippable item, so it already shrank via the item path (`ItemScale`); this toggle gates that independently so you can keep the cart full-size while other items shrink. Default on preserves prior behavior. Gated everywhere the cart could be re-shrunk (spawn, post-join rescale, un-equip) and host-synced to clients.

## 1.3.7
- Fix: held guns **and melee weapons** ride up to head height on ScalerCore 0.6.1. ScalerCore 0.6.1 added a second vertical patch — `ForceGrabPointVerticalScalePatch` — that shoves the grab puller up by `cameraUp * 0.3*(1-scale)` (≈ +0.18m at 0.4 scale) for any scaled player holding a force-grab-point item; both guns (`ItemGun`) and melee (`ItemMelee`) have one, so the lift stacks on top of the weapon's hold and parks it near the head. The 1.3.4/1.3.5 hold rework is self-contained (aim offset + restored grab drop) and doesn't want that lift, but nothing cancelled it. Now skip ScalerCore's raise for held guns and melee, and restore the game's proportional `-0.2 * scale` grab drop for both (previously gun-only). Valuable force-grab items (e.g. crystal ball) keep ScalerCore's lift. Earlier fixes were built against ScalerCore 0.5.x, which had no such patch.

## 1.3.6
- Fix: shrunk players can pistol-whip and melee-bonk others over again. The game's knockdown gate needs a held weapon's impact velocity ≥ 6, but a tiny player swings on a smaller radius so it never gets there. Scale the weapon's tracked impact velocity back up by ~1/scale (capped) for shrunk holders — applies to guns and melee/equippable weapons, and only affects the knock-or-not decision, not damage or break force. Carried valuables/props keep vanilla behaviour.

## 1.3.5
- Fix: held guns sit at hand height again instead of riding up near the head. The real cause was ScalerCore zeroing the gun's built-in downward grab offset (`grabVerticalOffset -0.2`) for shrunk players; 1.3.4's aim-pitch change couldn't affect height. Now restore a proportional drop (`-0.2 * scale`) for guns held by shrunk players.

## 1.3.4
- Fix: held guns no longer sit too high for shrunk players without SolidAim. Removed the puller "lift" (it over-corrected, compounding with ScalerCore zeroing the gun's built-in downward offset) and instead ported SolidAim's proven hold into our own stabilizer — sets the gun's `aimVerticalOffset` + a strong grab so guns sit at eye level standalone. Still defers entirely to SolidAim when it's installed.
- Fix: cart no longer makes shrunk players jitter fore-aft while pushing. The cart standoff is now aligned with ScalerCore's grab-beam distance (×0.7 at 0.4 scale) so cart-park and grab-pull agree instead of fighting along the push axis.

## 1.3.3
- Fix: held guns no longer sit higher than usual on pre-0.6.1 ScalerCore — our gun re-lift now scales by `0.3 * (1 - scale)` to match ScalerCore 0.6.1 instead of a flat `0.3` that fully cancelled the game's droop
- Fix: pushing a cart no longer recoils/shoves shrunk players — the cart's fixed 2–2.5m standoff now scales down with the grabber (ScalerCore only reduced it ~9%), so the cart hugs proportionally close instead of sweeping a full-size arc into the tiny player

## 1.3.2
- Fix: dying no longer un-shrinks the player (block ScalerCore's PlayerDeathExpandPatch, matching the existing damage-bonk block)
- Fix: held guns no longer shoot upward on ScalerCore 0.6.1+ — ScalerCore now lifts gun grab points itself, so our own lift only registers on older ScalerCore that lacks it
- Compat: adapt shop-level detection to the game update that made `RunManager.levelShop` a list (use `SemiFunc.IsLevelShop`)

## 1.0.0
- Initial release
- Shrinks all players, items, and valuables to configurable scale (default 0.4)
- REPOConfig compatible
