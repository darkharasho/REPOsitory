# Changelog

## 1.0.0
- Port of FullHealth 1.9.0 with the same settings and behavior.
- Round-start heals now run on the game's main thread. FullHealth healed from a background thread, which sent network messages off-thread and could freeze the game right after a level started.
