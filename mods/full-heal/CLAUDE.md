## Project Context

FullHeal: a port of yvsimakov/FullHealth 1.9.0 (MIT) with identical config/behavior, but heals run on the Unity main thread (original used Task.Run, which called Photon RPCs off-thread and froze the game at round start).
