# Changelog

## 0.1.0
- Initial release: take banded damage over time when not within a configurable
  distance of another living player. The farther past the safe radius, the
  bigger each tick. Host-authoritative; gameplay levels only; dead players are
  ignored as both anchors and targets.
- Config: `Enabled`, `SafeDistance`, `BandWidth`, `DamagePerBand`, `TickInterval`.
