# Changelog

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
