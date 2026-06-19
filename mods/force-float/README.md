# ForceFloat

Everyone floats — permanently. ForceFloat keeps every player in the lobby under the
game's Zero Gravity Staff effect for the whole level: you tumble, gravity is cancelled,
and you steer yourself through the air with your movement keys (look where you want to go).
It re-applies the effect before it wears off, so the float never ends.

Floating is active **only during levels**. The truck, shop, lobby, and menus play normally,
so you can still board, shop, and navigate without bouncing around.

It reproduces the Zero Gravity Staff's effect frame-for-frame on each player — the same
zero-gravity, drag, lift, drift-steering and wings the real staff applies — so it feels
exactly like the item, but always on.

## Config (BepInEx)

| Setting | Default | What it does |
|---------|---------|--------------|
| `Enabled` | `true` | Master on/off switch. Best set on the **host**, who drives the float for everyone. |
| `Flashlight` | `true` | Keep your flashlight on while floating (the game normally kills it during tumble). |

## Multiplayer

**The host runs the show.** Only the host (master client) spawns the float effect, because
the game simulates this physics on the host. That means:

- Only the **host** needs ForceFloat installed — everyone in the lobby floats regardless.
- The host's `Enabled` setting governs the whole lobby ("host wins").
- Dead players are left alone (no floating their head around).

## Install

Install via r2modman / Thunderstore. Requires BepInExPack 5.4.2100.
