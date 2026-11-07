# FullHeal

Restores players' health at the start of each level and, optionally, when they're revived.

This is [FullHealth](https://thunderstore.io/c/repo/p/yvsimakov/FullHealth/) 1.9.0 by yvsimakov, rebuilt for the current game. It has the same settings and does the same thing, with one fix: FullHealth healed from a background thread, which could freeze the game right after a level started. FullHeal heals on the game's main thread.

Don't run it alongside FullHealth. It refuses to load if FullHealth is installed.

## Configuration

Config file: `BepInEx/config/darkharasho.FullHeal.cfg`

The settings are identical to FullHealth's, so you can copy your old `FullHealth.cfg` over it.

| Section | Key | Default | Description |
|---|---|---|---|
| `General` / `Revival` | `Enabled` | `true` / `false` | Heal at round start / on revival. |
| | `ToMaxHealthPercentage` | `100` | Heal up to this % of max health. |
| | `ByMaxHealthPercentage` | `0` | Heal by this % of max health. Overrides the above. |
| | `ExactValue` | `0` | Heal exactly this much. Overrides the above. |
| | `HealthPackMode` | `None` | Heal a random health-pack amount (`Small` 25, `Medium` 50, `Large` 100). Overrides the above. |
| | `Expression` | empty | A formula for the heal amount ([Expressive](https://github.com/bijington/expressive) syntax). Overrides everything. Variables and examples are in the config file. |
| | `WorkMode` | `Host` | Who runs the heal: `Host`, `Client` or `All`. |
| | `HealMode` | `All` | Heal `Self`, `Others` or `All`. |
| | `PhaseMode` | `All` | Where to heal: `Level`, `Shop`, `Lobby`, `Arena`, `All`. Pick only one of `Level` or `Lobby`, or round-start heals happen twice. |
| `General` | `HealRequirementMode` | `None` | `Survive`: only heal players who made it back to the truck alive. |

## Multiplayer

With the default `WorkMode = Host`, only the host needs the mod.

## Installation

Install with [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) or r2modman. To install by hand, put `FullHeal.dll` and `Expressive.dll` in `BepInEx/plugins/FullHeal/`.

## Credits

Original mod and design: yvsimakov ([R.E.P.O_FullHealth](https://github.com/yvsimakov/R.E.P.O_FullHealth), MIT).
