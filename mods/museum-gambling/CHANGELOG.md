# Changelog

## 0.1.0
- Initial release.
- Gamble the Museum Head: clicking it has a configurable percent chance to spawn a money-bag valuable instead of dealing the head's normal damage.
- Config: `Enabled` (default `true`), `WinChancePercent` (default `5`, range `0–100`), `PayoutValue` (default `50000`, range `0–1,000,000`).
- Host-authoritative roll; result broadcast to all clients via `PhotonNetwork.RaiseEvent` so the clicking player's damage is suppressed when the host's roll is a win.
