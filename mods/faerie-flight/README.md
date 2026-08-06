# Faerie Flight

> **⚠️ 0.2.3 and older can freeze/black-screen the game during loading — update to 0.2.4.**
> Older versions could touch the item database mid-scene-transition (a synchronous bundle
> load), which can hang the main thread while creating a lobby, leaving to the menu, or
> entering a level — especially in big modded packs. 0.2.4 defers all of that until the
> level has fully finished loading and is a drop-in update — get everyone in the lobby onto
> it. Also run Overstaffed 0.2.0+ or none (older Overstaffed broke loading whenever any new
> mod was added).

Sprout wings and drift. Faerie Flight keeps every player under the game's Zero Gravity
Staff effect for the whole level — you tumble weightlessly, little wings appear, and you
steer through the air with your movement keys (look where you want to go). The effect is
refreshed before it ever wears off, so the float never ends.

Floating is active **only during levels**. The truck, shop, lobby, and menus play normally,
so you can still board, shop, and navigate without drifting around.

It reproduces the Zero Gravity Staff's effect frame-for-frame on each player — the same
zero-gravity, drag, lift, drift-steering and wings the real staff applies — so it feels
exactly like the item, but always on. Your flashlight stays lit the whole time (the game
normally cuts it during tumble).

## Config (BepInEx)

| Setting | Default | What it does |
|---------|---------|--------------|
| `Enabled` | `true` | Master on/off switch. Best set on the **host**, who drives the float for everyone. |
| `Flashlight` | `true` | Keep your flashlight on while floating (the game normally kills it during tumble). |
| `RestoreGrabUpgrades` | `true` | The game ignores Strength upgrades while tumbling, which permanent float would make permanent. Restores them (softened) while floating. Host-side; grabbing enemies keeps vanilla behavior. |
| `StrengthSoftening` | `0.65` | How much to soften restored Strength upgrades (0 = full strength, 1 = square-root curve). Floating grabbers take the grab's recoil, so full strength can fling you. |

## Multiplayer

Install it on **every** player in the lobby for the full experience. Under the hood the
effect is master-driven — the host simulates the float physics and steering for everyone
(your movement input reaches the host through the game's own replication), and the host's
`Enabled` setting governs the lobby. A player without the mod should still float and steer;
what they lose is the local polish (the zero-gravity particles and the flashlight staying
lit). Same-version-everywhere remains the recommended, tested setup. Dead players are left
alone (no floating their head around).

## Install

Install via r2modman / Thunderstore. Requires BepInExPack 5.4.2100.
