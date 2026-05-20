# Overstaffed

Raises R.E.P.O.'s max player count above the vanilla 6. Configurable from 1 to 20.

Replacement for the unmaintained `Spindles-MorePlayersImproved` on R.E.P.O. 0.4.0+.

## Configuration

After first launch, edit `BepInEx/config/darkharasho.Overstaffed.cfg`:

```
[General]

## The maximum number of players allowed in a server.
# Setting type: Int32
# Default value: 10
# Acceptable value range: From 1 to 20
MaxPlayers = 10
```

> ⚠️ Values above ~10 may exhibit Photon networking instability — Photon's per-room player ceiling isn't designed for very high counts. Both host and joining players need this mod installed to use a raised cap.
