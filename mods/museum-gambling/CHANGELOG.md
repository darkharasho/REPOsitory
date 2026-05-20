# Changelog

## 0.1.4
- Win/loss is no longer instantly readable from damage. On a win, the head's hurt collider is temporarily neutered (`playerKill=false`, `playerDamage=1`) so the vanilla hit reaction still fires (blood, flash, sound, tumble), then the player is silently healed to full in a postfix. Loss still kills as before — the payout that appears when the head re-opens is the only outcome cue during the close.
- Anti-cheat: players were hiding under the ledge beneath the head so the drag-in force couldn't reach them; the head still closed and they'd win the payout without taking the risk. The host now does a `Physics.OverlapBox` against the head's `boxColliderCheckTransform` at the `Closed` transition and only rolls if the grabbed player's `PhysGrabObject` is actually inside the mouth. No box → no roll → no payout.

## 0.1.3
- Fix: every non-host player was kicked to the lobby when an ally was sucked into the head. The win/loss broadcast used Photon event code `199`, which REPO itself uses as its "you were kicked by the host" signal. Moved the broadcast to event code `173`.

## 0.1.2
- Fix: lobby region picker hanging on game launch. Photon event subscription was happening too early (BepInEx plugin `Awake`), interfering with REPO's Photon initialization. Subscription is now deferred until first museum-head interaction, when Photon is fully ready.

## 0.1.1
- New icon.

## 0.1.0
- Initial release.
- Gamble the Museum Head: clicking it has a configurable percent chance to spawn a money-bag valuable instead of dealing the head's normal damage.
- Config: `Enabled` (default `true`), `WinChancePercent` (default `5`, range `0–100`), `PayoutValue` (default `50000`, range `0–1,000,000`).
- Host-authoritative roll; result broadcast to all clients via `PhotonNetwork.RaiseEvent` so the clicking player's damage is suppressed when the host's roll is a win.
