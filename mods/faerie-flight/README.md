# Faerie Flight

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

## Multiplayer

Install it on **every** player in the lobby. Each client engages the effect on its own
avatar, while the host (master client) simulates the floating physics for everyone — the
host's `Enabled` setting governs the lobby. Dead players are left alone (no floating their
head around).

## Install

Install via r2modman / Thunderstore. Requires BepInExPack 5.4.2100.
