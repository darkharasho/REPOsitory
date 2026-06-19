# Changelog

## 0.3.0
- Host config sync: the gameplay rule (`Enabled`, `AnchorMode`, `SafeDistance`, `BandWidth`,
  `DamagePerBand`, `TickInterval`) is taken from the host and synced to all clients, so beams
  match the host-authoritative damage. Beam display prefs stay local.
- Truck safe zone: a player standing in the extraction truck takes no damage regardless of
  distance (and counts as safe — green beam in Cart mode, hidden in Buddy mode).
- Cart mode now anchors each player to the **nearest** cart, supporting multiple medium carts.
- Buddy mode hides the beam when you're safe — split groups that are each fine show no lines;
  the tether only appears in the warn/danger zone.
- Beams restyled thinner with an additive glow to mimic the game's grab beam; new `Beams/Width`.
- All numeric settings are now whole integers with sane maximums; `SafeDistance` default is 20,
  `WarnPercent` is now a 0–100 percent.

## 0.2.0
- New `AnchorMode` setting: `Buddy` (stay near the nearest living player, the original
  behavior) or `Cart` (stay near the main hauling cart). Cart mode falls back to the
  buddy rule until a cart exists in the level.
- Tether beams: a colored line is drawn from each player to their anchor —
  green when safe, yellow approaching the edge, red while taking damage.
- Config: `Beams/Enabled`, `Beams/ShowAllPlayers`, `Beams/WarnPercent`.

## 0.1.0
- Initial release: take banded damage over time when not within a configurable
  distance of another living player. The farther past the safe radius, the
  bigger each tick. Host-authoritative; gameplay levels only; dead players are
  ignored as both anchors and targets.
- Config: `Enabled`, `SafeDistance`, `BandWidth`, `DamagePerBand`, `TickInterval`.
