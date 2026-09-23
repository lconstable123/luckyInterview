# Lucky Robots Test Scene

A LuckyEngine scene where two Franka Panda arms cooperate to pick up a cable and plug it into a port.

## Task

- **Objective:** retrieve a cable from a shelf and insert it into a target port.
- **Robots:** dual Panda arms (`Panda_l` left, `Panda_r` right), each MuJoCo-driven.
- One arm grabs and pulls the cable, hands it off to the other, which reorients and plugs it in.

## Key scripts

- [`Take_ServerPlugger.cs`](Assets/Scripts/Client/Source/Take_ServerPlugger.cs) - source of truth for arm state control. Dispatches queued motion steps to each arm via `Panda_Arm_ServerController`. Written with limited use of AI.

- [`Panda_Arm_ServerController.cs`](Assets/Scripts/Client/Source/Panda_Arm_ServerController.cs) - per-arm controller, derived from the shared [`PandaController.cs`](Assets/Scripts/Client/Source/PandaController.cs).

- [`Waypoint.cs`](Assets/Scripts/Client/Source/Waypoint.cs) - tagged scene waypoints the arms move between.
- [`LuckyHubEpisodeManager.cs`](Assets/Scripts/Source/LuckyHubEpisodeManager.cs) - Lucky Hub wrapper, completley vibe conded, I had trouble uploading it to locky hob adding observations/rewards for RL training (see [`Assets/README_LuckyHub.md`](Assets/README_LuckyHub.md)).
- [`EpisodeManager.cs`](Assets/Scripts/Client/Source/EpisodeManager.cs) - reusable start/timeout/complete/restart lifecycle used by `Take_ServerPlugger` to run the pick-and-plug sequence as a repeatable episode, again all vibe coded.

## Running it

1. Open the project in LuckyEngine.
2. Press Play - the episode starts automatically via `Take_ServerPlugger.OnCreate`.
