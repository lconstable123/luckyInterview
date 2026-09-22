using System;
using Hazel;
using System.Collections.Generic;

namespace LuckyInterview
{

	public class Take_ServerPlugger : Entity
	{
		[Group("Scene Components")]
		public Entity? waypointsFolder = null;
		[Group("Scene Components")]
	
		public Entity? ArmLeft = null;
		[Group("Scene Components")]
		public Entity? ArmRight = null;

		[Group("Scene Components")]
		public Entity? Cable = null;


		//-----------------------------------------------------------------------------------
		[Group("Episode Settings")]
		public float episodeTimeoutSeconds = 60.0f;
		[Group("Episode Settings")]
		public bool resetOnCompletion = true;

		//-----------------------------------------------------------------------------------

		[Group("Movement settings")]
		public float movementSpeed = 100.0f;
		[Group("Movement settings")]
		public float rotationSpeed = 100.0f;
		[Group("Movement settings")]
		public float taskDelay = 1.0f;
		[Group("Pole Vector Settings (Left, mirrored)")]
		public Vector3 poleVectorArmL = new(-0.2f, 0.01f, 1f);
		[Group("Left Grip positon offset)")]
		public Vector3 leftGripOffset = new Vector3(0f, 0f, 0.2f);
	


		//-----------------------------------------------------------------------------------
		[Group("Cable settings")]
		public float cableGripDistfromEndClose = 0.04f;
		[Group("Cable settings")]
		public float cableGripDistfromEndFar = 0.17f;
		[Group("Cable settings")]
		public Vector3 cableAxis = new Vector3(0f, 0f, 1f);
		[Group("Cable settings")]
		public float pullDistance = 0.5f;

		//-----------------------------------------------------------------------------------
		// storing each arm's targets in the scene as a key to a transform
		private Dictionary<WaypointFlag,Transform> _waypointPositions = [];
		private Transform _currentCableTransform = new();
		// store cast to script for each arm controller
		private Panda_Arm_ServerController? _armL = null;
		private Panda_Arm_ServerController? _armR = null;

		private Vector3 _currentArmRotR = new();
		private Vector3 _currentArmPosR = new();
		private Vector3 _currentArmRotL = new();
		private Vector3 _currentArmPosL = new();

		private EpisodeManager? _episode;

		protected override void OnCreate()
		{

			bool fieldsValid = FindAndStoreFields();
			if (!fieldsValid) return;
			bool waypointsFound = FindAndStoreWaypoints();
			if (!waypointsFound) return;
			_episode = new EpisodeManager(PlayEpisode, episodeTimeoutSeconds, resetOnCompletion, InterruptArms);
			// Runs automatically as soon as Play starts - no manual trigger needed.
			_episode.Start();
		}

		protected override void OnDestroy()
		{
			_episode?.Shutdown();
		}

		[Button("Start Episode Now")]
		[Tooltip("Interrupt any running episode and begin a fresh one immediately.")]
		public void StartEpisode() => _episode?.Start();

		private void InterruptArms()
		{
			_armL?.ClearQueue();
			_armR?.ClearQueue();
		}

		private void PlayEpisode()
		{
			MoveArmToCenter(ArmSide.Right);
			_armR!.EnqueueOpenGripper(.1f);
			_armL!.EnqueueOpenGripper(.1f);
			MoveArmToAssembly(ArmSide.Left);
			GrabCableFromShelf(ArmSide.Right);
			PullCable(ArmSide.Right);
			MoveArmToAssembly(ArmSide.Right);
			_armR!.EnqueueAction(()=>ReceiveAndReorientCable(ArmSide.Left));
		}

		private Transform GetCableWorldGripOffset(Transform cableTransform, CableGripPos gripPos, float topOffset)
		{
			Vector3 cableWorldPos = cableTransform.Position;
			Vector3 cableWorldRot = cableTransform.Rotation;

			// thinking of a matrix world to local and back
			// or using atan2 to get the angle and then rotate the offset accordingly,
			// but ultimatley I just can copy the rotation of the cable world => world.
	
			float gripDist = gripPos == CableGripPos.Front ? cableGripDistfromEndClose : cableGripDistfromEndFar;
			
			Vector3 cableWorldPosOffset = cableWorldPos + cableAxis.Normalized() * -gripDist;
			cableWorldPosOffset.Y -= topOffset;
            Transform offsetWorldTransform = new Transform
            {
                Position = cableWorldPosOffset,
                Rotation = cableWorldRot
            };

            return offsetWorldTransform;
		}

		

		private void GrabCableFromShelf(ArmSide armSide)
		{
			var (armController, targetGripPos) = GetRelevantArmAndGripPos(armSide);
			armController!.EnqueueOpenGripper(.1f);
			Log.Info("Grabbing cable from shelf.");
			_currentCableTransform = GetEntityTransform(Cable!);
			Transform offsetWorldTransform = GetCableWorldGripOffset(_currentCableTransform, CableGripPos.Front, 0.05f);
			Vector3 offsetGripRot = offsetWorldTransform.Rotation;
			offsetGripRot.X += Mathf.PIonTwo;
			offsetGripRot.Y += Mathf.PIonTwo;
			armController!.EnqueueMoveAtSpeed(offsetWorldTransform.Position, offsetGripRot, movementSpeed*.8f);
			StoreCurrentArmTransform(armSide, offsetWorldTransform.Position, offsetGripRot);
			armController!.EnqueueCloseGripper(.1f);
			armController!.EnqueueWait(.3f);

		}

		private void ReceiveAndReorientCable(ArmSide armSide)
		{
			// toDo- cleanup and refactor
			Log.Info("Receiving cable and reorientating it.");
			var (armController, targetGripPos) = GetRelevantArmAndGripPos(armSide);
			_currentCableTransform = GetEntityTransform(Cable!);
			Transform targetWorldTransform = GetCableWorldGripOffset(_currentCableTransform, targetGripPos,0.0f);
			targetWorldTransform.Position += leftGripOffset;
			Vector3 targetRot = _currentCableTransform.Rotation + new Vector3(Mathf.PIonTwo, 0, Mathf.PIonTwo);
			_armL!.EnqueueMoveAtSpeed(targetWorldTransform.Position, _currentArmRotL, 0.2f);
			StoreCurrentArmTransform(armSide, targetWorldTransform.Position, targetRot);
			TransferCable(ArmSide.Left);
			PlugCable(ArmSide.Left);
		}

		private void TransferCable(ArmSide fromArmSide)
		{
			//toDo refactor into side-agnostic
			_armL!.EnqueueAction(() => _armR!.EnqueueOpenGripper(.1f));
			_armL!.EnqueueWait(.1f);
			 _armL!.EnqueueCloseGripper(.1f);
			_armL.EnqueueAction(() => 
			{
				_armR!.EnqueueMoveAtSpeed(_waypointPositions[WaypointFlag.start_right].Position, 
				_waypointPositions[WaypointFlag.neutral_right].Rotation, movementSpeed);
			});
		}

		private void PlugCable(ArmSide armSide)
		{
			//toDo refactor into side-agnostic
			_armL!.EnqueueMoveAtSpeed(_currentArmPosL+new Vector3(0f, 0f, 1f)*.2f, _currentArmRotL, 0.1f);
			_armL!.EnqueueMoveAtSpeed(_waypointPositions[WaypointFlag.insert].Position, _waypointPositions[WaypointFlag.insert].Rotation, 2f);
			_armL!.EnqueueOpenGripper(.1f);
			 _armL!.EnqueueMoveAtSpeed(_waypointPositions[WaypointFlag.start_left].Position, _waypointPositions[WaypointFlag.start_left].Rotation, 1f);
			_armL!.EnqueueAction(() => _episode?.Complete());

		}

		private void PullCable(ArmSide armSide)
		{
			Log.Info("Pulling cable.");
			var (armController, targetGripPos) = GetRelevantArmAndGripPos(armSide);
			_currentCableTransform = GetEntityTransform(Cable!);
			Vector3 pullPosition = armSide == ArmSide.Right ? _currentArmPosR + cableAxis.Normalized() * pullDistance : _currentArmPosL + cableAxis.Normalized() * pullDistance;
			Vector3 pullRot = armSide == ArmSide.Right ? _currentArmRotR : _currentArmRotL;
			armController!.EnqueueMoveAtSpeed(pullPosition, pullRot, .2f);

		}

		private void MoveArmToCenter(ArmSide armSide)
		{
			Log.Info("Moving arm to center.");
			var (armController, targetGripPos) = GetRelevantArmAndGripPos(armSide);
			
			Transform centerTransform = new()
            {
				Position = _waypointPositions[WaypointFlag.neutral_center].Position,
				Rotation = _waypointPositions[WaypointFlag.neutral_center].Rotation
			};
			armController!.EnqueueMoveAtSpeed(centerTransform.Position, centerTransform.Rotation, movementSpeed);
			StoreCurrentArmTransform(armSide, centerTransform.Position, centerTransform.Rotation);
		}

		private void MoveArmToAssembly(ArmSide armSide)
		{
			Log.Info("Moving arm to assembly position.");
			var (armController, targetGripPos) = GetRelevantArmAndGripPos(armSide);
			
			var ArmAppropriateWaypoint = armSide == ArmSide.Left ? WaypointFlag.neutral_left : WaypointFlag.neutral_right;
			Transform assemblyTransform = new()
            {
				Position = _waypointPositions[ArmAppropriateWaypoint].Position,
				Rotation = _waypointPositions[ArmAppropriateWaypoint].Rotation
			};
			StoreCurrentArmTransform(armSide, assemblyTransform.Position, assemblyTransform.Rotation);
			armController!.EnqueueMoveAtSpeed(assemblyTransform.Position, assemblyTransform.Rotation, movementSpeed/4);
		}

		private bool FindAndStoreFields()
		{
				if (waypointsFolder is null || 
					ArmLeft is null || 
					ArmRight is null ||
					Cable is null)
				{
					Log.Error("Some required fields are not set in Take_ServerPlugger.");
					return false;
				}

				_armL = ArmLeft!.As<Panda_Arm_ServerController>()!;
				_armR = ArmRight!.As<Panda_Arm_ServerController>()!;
				Log.Info("Stored arm controllers successfully.");
				return true;
		}

		private Panda_Arm_ServerController GetPandaArmController(ArmSide armSide)
		{
   			 return armSide == ArmSide.Left ? _armL! : _armR!;
		}

		private static Transform GetEntityTransform(Entity entity)
		{
    		Transform  transform = entity.Transform.WorldTransform;
			Log.Info($"Retrieved entity transform: {transform.Position}, {transform.Rotation}");
			return transform;
		}

		public (Panda_Arm_ServerController armController, CableGripPos targetGripPos) GetRelevantArmAndGripPos(ArmSide armSide)
		{
			var armController = GetPandaArmController(armSide);
			var targetGripPos = armSide == ArmSide.Left ? CableGripPos.Back : CableGripPos.Front;
			return (armController, targetGripPos);
		}

		private void StoreCurrentArmTransform(ArmSide armSide, Vector3 position, Vector3 rotation)
		{
			var armController = GetPandaArmController(armSide);
			if (armSide == ArmSide.Right)
			{
				_currentArmRotR = rotation;
				_currentArmPosR = position;
			}
			else
			{
				_currentArmRotL = rotation;
				_currentArmPosL = position;
			}
		}


		private bool FindAndStoreWaypoints()
		{
			if (waypointsFolder is not null && waypointsFolder.Children.Length > 0)
			{
				Log.Info("Finding waypoints in waypointsFolder: " + waypointsFolder);
				foreach (var child in waypointsFolder!.Children)
				{
					if (child is Entity entity && entity.As<Waypoint>() is Waypoint waypoint)
					{
						_waypointPositions[waypoint.waypointFlag] =  waypoint.GetTransform();
					}
				}
				if (!AreAllWaypointsFound())
				{
					Log.Error("Not all waypoints were found in Take_ServerPlugger.");
					return false;
				} else {
					Log.Info("All waypoints found successfully.");
					return true;
				}
			}
			return false;
		}

		private bool AreAllWaypointsFound()
		{
			foreach (var flag in Enum.GetValues<WaypointFlag>())
			{
				if (!_waypointPositions.ContainsKey((WaypointFlag)flag))
				{
					Log.Error("Waypoint not found: " + flag);
					return false;
				}
			}
			Log.Info("All waypoints verified successfully.");
			return true;
		}

		public enum CableGripPos
{
    	Front,
		Center,
    	Back
}

	public enum ArmSide
	{
		Left,
		Right
	}





	}
}
