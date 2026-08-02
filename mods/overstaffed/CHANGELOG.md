# Overstaffed Changelog

## 0.2.0

- Fix the `NetworkConnect.OnConnectedToMaster` transpiler dropping the branch label on the
  replaced `ldc.i4.6`, which made the patch fail to compile (`Label #N is not marked`) and
  left a broken Harmony wrapper that hard-froze loading when another Harmony mod forced a
  wrapper recompile. The instruction is now mutated in place, preserving its labels/blocks.

## 0.1.0

- Initial release. Raises R.E.P.O. 0.4.0's max player count above 6 (configurable 1–20).
- Replaces the unmaintained `Spindles-MorePlayersImproved`, whose patch targets no longer exist in 0.4.0.
