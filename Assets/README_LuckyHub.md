# Dual Panda Cable Plugging Task - Lucky Hub Environment

## Overview
This environment features two Panda robot arms working together to perform a cable insertion task. One arm retrieves and manipulates the cable while the other assists with positioning and handoff.

## Task Description
- **Objective**: Insert a cable into a target port
- **Robots**: Dual Franka Panda arms (left and right)
- **Success Criteria**: Cable positioned within 5cm of target port
- **Time Limit**: 120 seconds per episode

## Key Components

### Scene Structure
- `Main.hscene` - Main scene file with all entities
- Dual Panda robots with MuJoCo physics
- Cable object (manipulable)
- Target port (static)
- Waypoint system for robot navigation
- Observation cameras (overview + task-focused)

### Scripts
- `Take_ServerPlugger.cs` - Main task controller with pre-programmed sequence
- `LuckyHubEpisodeManager.cs` - Episode management, observations, rewards
- `Panda_Arm_ServerController.cs` - Individual robot arm control

### Episode Management
- **Start**: Scene reset to initial positions
- **Progress**: Continuous reward for moving cable closer to target
- **Success**: Cable within 5cm of target port (+100 reward)
- **Failure**: Timeout after 120 seconds (-10 penalty)
- **Auto-reset**: Automatically starts new episode after completion

## Observation Space
- Cable position and rotation (world coordinates)
- Target port position and rotation
- Distance to target
- Episode elapsed time
- Camera feeds from two viewpoints

## Action Space
The current implementation uses a pre-programmed sequence (`Take_ServerPlugger`). For RL training, the action space could be extended to:
- Joint position/velocity commands for both arms
- Gripper open/close commands
- High-level waypoint targets

## Reward Structure
- **Success**: +100 points for successful cable insertion
- **Progress**: +1 point per cm moved closer to target
- **Time**: -0.01 points per timestep (efficiency incentive)
- **Timeout**: -10 points for episode timeout

## Usage Instructions

### For Lucky Hub Upload
1. Package the entire project directory
2. Include `LuckyHubConfig.json` configuration
3. Ensure all entity references are correct
4. Test episode functionality before upload

### Local Testing
1. Open scene in LuckyEngine
2. Press Play to start simulation
3. Use Episode Manager buttons:
   - "Start Episode" - Initialize new episode
   - "Reset Scene" - Return to initial state
   - "Check Episode Status" - Manual status check
   - "Log Current Observations" - Debug output

### Configuration Files
- `LuckyHubConfig.json` - Environment specification for Lucky Hub
- Entity UUIDs are pre-configured in the JSON

## Technical Notes
- Uses MuJoCo physics for robot simulation
- Jolt physics for non-robot objects
- Waypoint-based robot navigation
- Structured logging for training data capture
- Modular design allows easy modification of task parameters

## Customization
To modify the task:
1. Adjust reward parameters in `LuckyHubEpisodeManager`
2. Modify success criteria (distance threshold, time limit)
3. Add new observation data in `GetObservations()`
4. Update waypoints for different robot trajectories
5. Change object positions/properties as needed

## Dependencies
- LuckyEngine with MuJoCo support
- Panda robot models
- Required scripts compiled and loaded