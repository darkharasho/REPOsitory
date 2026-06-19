# ForceFloat

Everyone floats — permanently. ForceFloat keeps every player in the lobby under the
game's Zero Gravity Staff effect for the whole level: you tumble, gravity is cancelled,
and you steer yourself through the air with your movement keys (look where you want to go).
It re-applies the effect before it wears off, so the float never ends.

Floating is active **only during levels**. The truck, shop, lobby, and menus play normally,
so you can still board, shop, and navigate without bouncing around.

It works by spawning the game's own Zero Gravity Staff effect on each player, the exact
same one the staff produces — so the float, drift steering, and wings all behave just
like the real item.

## Config (BepInEx)

| Setting | Default | What it does |
|---------|---------|--------------|
| `Enabled` | `true` | Master on/off switch. Only the **host's** value matters. |

## Multiplayer

**The host runs the show.** Only the host (master client) spawns the float effect, because
the game simulates this physics on the host. That means:

- Only the **host** needs ForceFloat installed — everyone in the lobby floats regardless.
- The host's `Enabled` setting governs the whole lobby ("host wins").
- Dead players are left alone (no floating their head around).

## Install

Install via r2modman / Thunderstore. Requires BepInExPack 5.4.2100.
