# Lucky Robots Test Scene

A LuckyEngine test scene, i'm leanring the ropes.

## Task

- **Objective:** retrieve a cable from a shelf and insert it into a target port.
- **Robots:** dual Panda arms (`Panda_l` left, `Panda_r` right).
- One arm grabs and pulls the cable, hands it off to the other, which reorients and plugs it in.

## Key scripts

- [`Take_ServerPlugger.cs`](Assets/Scripts/Client/Source/Take_ServerPlugger.cs) - **Written with limited use of AI.** source of truth for arm state control and task flow. Dispatches queued motion steps to each arm via `Panda_Arm_ServerController`.

- [`Panda_Arm_ServerController.cs`](Assets/Scripts/Client/Source/Panda_Arm_ServerController.cs) - per-arm controller, derived from the shared [`PandaController.cs`](Assets/Scripts/Client/Source/PandaController.cs).

- [`Waypoint.cs`](Assets/Scripts/Client/Source/Waypoint.cs) - tagged scene waypoints the arms move between.

## Running it

1. Open the project in LuckyEngine.
2. Press Play - the episode starts automatically via `Take_ServerPlugger.OnCreate`.
