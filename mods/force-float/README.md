# ForceFloat

Everyone floats — permanently. ForceFloat keeps every player in the lobby under the
game's Zero Gravity Staff effect for the whole level: you tumble, gravity is cancelled,
and you steer yourself through the air with your movement keys (look where you want to go).

Floating is active **only during levels**. The truck, shop, lobby, and menus play normally,
so you can still board, shop, and navigate without bouncing around.

## Config (BepInEx)

| Setting | Default | What it does |
|---------|---------|--------------|
| `EnableDrift` | `true` | Steer through the air with movement keys. Off = pure ragdoll drift. |
| `Wings` | `true` | Show the tumble wing visuals. |

## Multiplayer

Client-side. Each player who installs it floats themselves. The host (master client)
additionally tumbles everyone in the lobby, so players without the mod also float
(they ragdoll; only modded players get steering).

## Install

Install via r2modman / Thunderstore. Requires BepInExPack 5.4.2100.
