using System;
using System.Collections.Generic;
using Hazel;

namespace LuckyInterview
{
	// Pre-calibrated pick-and-place controller. Target_Position is the
	// grasp_site (the robot's motion graph End Effector). GraspOffsetY
	// trims the gap between the site and the actual fingertip mid-pad.
	// Override ComposeEpisode() with your per-block pickup/placement queue.
	public class PandaController : Entity
	{
		// ── Scene wiring ──────────────────────────────────────────────────
		[Group("Robot Mapping")] [Tooltip("MuJoCo actuator that drives the gripper open/close.")]
		public string GripperActuatorName = "gripper";

		// ── Home / approach geometry ──────────────────────────────────────
		[Group("Home Pose")] [Units("m")]
		[Tooltip("World position the gripper grasp_site idles at.")]
		public Vector3 Home          = new Vector3(0.30f, 0.30f, 0.0f);

		[Group("Grasp & Approach")] [Units("m")] [Slider(0.05f, 0.40f)]
		[Tooltip("Hover height above the block centre during pre-grasp/post-place.")]
		public float PreGraspLiftY    = 0.10f;

		[Group("Grasp & Approach")] [Units("m")] [Slider(-0.05f, 0.10f)]
		[Tooltip("Signed offset added to Target_Position.Y at grasp/release. " +
			"TCP convention (Piper grasp_site at link6 z=0.135 = pad far edge / fingertip): " +
			"this offset directly controls fingertip depth relative to the block centre.\n" +
			"  0          → fingertip at block centre (default, fingers wrap upper half — good general grip)\n" +
			"  -blockH/2  → fingertip at block bottom (fingers wrap whole block; floor-safe for ground-resting blocks)\n" +
			"  +blockH/2  → fingertip at block top (fingers don't descend into block — wrong for grasping).\n" +
			"For 25 mm blocks: 0 = halfway down, -0.0125 = at block bottom.")]
		public float GraspOffsetY = 0.0f;

		[Group("Grasp & Approach")] [Units("m")] [Slider(0f, 0.4f)]
		[Tooltip("Extra altitude for the lateral leg of a routed approach (lift here → translate over → descend).")]
		public float SafeApproachLift = 0.10f;

		[Group("Grasp & Approach")] [Units("rad")] [Slider(-3.15f, 3.15f)]
		[Tooltip("Extra yaw added to align the gripper opening axis with the block's SHORT axis. " +
			"If the gripper closes across the LONG axis, flip this by ±π/2 (~1.5708).")]
		public float GripperYawOffset = Mathf.PIonTwo;

		// ── Motion speeds ────────────────────────────────────────────────
		[Group("Motion Speeds")] [Units("m/s")] [Slider(0.05f, 2.0f)]
		public float TravelSpeed       = 0.5f;
		[Group("Motion Speeds")] [Units("m/s")] [Slider(0.02f, 1.5f)]
		public float DescendSpeed      = 0.2f;
		[Group("Motion Speeds")] [Units("m/s")] [Slider(0.02f, 1.0f)]
		public float PlaceDescendSpeed = 0.08f;
		[Group("Motion Speeds")] [Units("rad/s")] [Slider(0.2f, 5.0f)]
		public float MaxAngularSpeed   = 1.5f;
		[Group("Motion Speeds")] [Units("s")] [Slider(0.02f, 1.0f)]
		public float MinMoveDuration   = 0.15f;
		[Group("Motion Speeds")] [Units("s")] [Slider(0.0f, 2.0f)]
		public float AlignDwell        = 0.2f;

		// ── Gripper ──────────────────────────────────────────────────────
		[Group("Gripper")] [ClampValue(0f, 255f)]
		public float GripperOpenValue  = 255f;
		[Group("Gripper")] [ClampValue(0f, 255f)]
		public float GripperCloseValue = 0f;
		[Group("Gripper")] [Units("s")] [Slider(0.05f, 5.0f)]
		public float GripperSettle     = 1.0f;

		// ── Startup ──────────────────────────────────────────────────────
		[Group("Startup")] [Units("s")] [Slider(0f, 5f)]
		[Tooltip("Delay after Play before the first queued motion fires.")]
		public float StartupDelay      = 1.0f;

		// ── Graph input identifiers — MUST match the robot's hmograph ────
		private static readonly Identifier k_Pos = new Identifier("Target_Position");
		private static readonly Identifier k_Rot = new Identifier("Target_Orientation");
		private static readonly Identifier k_Dur = new Identifier("Duration");
		private static readonly Identifier k_Go  = new Identifier("Start");

		// ── Runtime (do not edit) ────────────────────────────────────────
		[HideFromEditor] private RobotControllerComponent? m_Robot;
		[HideFromEditor] private MujocoSceneComponent?     m_Mujoco;
		[HideFromEditor] private MujocoActuator?           m_Gripper;
		[HideFromEditor] private float m_StartupTimer;

		private struct Step { public Action? OnStart; public float Duration; }
		[HideFromEditor] private readonly Queue<Step> m_Queue = new Queue<Step>();
		[HideFromEditor] private float m_Elapsed;
		[HideFromEditor] private bool  m_Started;
		[HideFromEditor] private Vector3 m_PlannedPos;
		[HideFromEditor] private Vector3 m_PlannedRot;

		public bool IsBusy => m_Queue.Count > 0;
		public Vector3 PlannedEndPosition => m_PlannedPos;

		public static readonly Vector3 TopDownRotIdentity = new Vector3(Mathf.PIonTwo, 0f, 0f);

		// Override with the per-block EnqueuePickup / EnqueuePlacement / EnqueueGoHome sequence.
		protected virtual void ComposeEpisode()
		{
			EnqueueGoHome();
		}

		// ──────────────────────────────────────────────────────────────────
		//  Lifecycle
		// ──────────────────────────────────────────────────────────────────
		protected override void OnCreate()
		{
			m_Robot   = GetComponent<RobotControllerComponent>();
			m_Mujoco  = GetComponent<MujocoSceneComponent>();
			m_Gripper = m_Mujoco != null ? m_Mujoco.GetActuator(GripperActuatorName) : null;
			if (m_Gripper != null) { m_Gripper.IK = false; m_Gripper.Value = GripperOpenValue; }
			// if (m_Gripper == null)
			// {
			// 	Log.Critical("Gripper actuator not found.");
			// } else
			// {
			// 	Log.Info("Gripper actuator found successfully.");
			// }
			m_PlannedPos   = Home;
			m_PlannedRot   = TopDownRotIdentity;
			m_StartupTimer = StartupDelay;

			ComposeEpisode();
		}

		protected override void OnUpdate(float ts)
		{
			if (m_StartupTimer > 0f) { m_StartupTimer -= ts; return; }
			if (m_Queue.Count == 0) return;
			if (!m_Started)
			{
				var head = m_Queue.Peek();
				if (head.OnStart != null) head.OnStart.Invoke();
				m_Started = true;
				m_Elapsed = 0f;
			}
			m_Elapsed += ts;
			if (m_Elapsed >= m_Queue.Peek().Duration) { m_Queue.Dequeue(); m_Started = false; }
		}

		// ──────────────────────────────────────────────────────────────────
		//  Public queue API — ONE SetInputTrigger per step. NEVER two.
		// ──────────────────────────────────────────────────────────────────
		public void EnqueueMoveAtSpeed(Vector3 pos, Vector3 rotRad, float speed)
		{
			float dist = Vector3.Distance(pos, m_PlannedPos);
			// Largest per-axis delta (wrapped to ±π).
			float dx = Mathf.Abs(Mathf.WrapToPi(rotRad.X - m_PlannedRot.X));
			float dy = Mathf.Abs(Mathf.WrapToPi(rotRad.Y - m_PlannedRot.Y));
			float dz = Mathf.Abs(Mathf.WrapToPi(rotRad.Z - m_PlannedRot.Z));
			float rotMag = Mathf.Max(dx, Mathf.Max(dy, dz));
			float dur    = Mathf.Max(MinMoveDuration,
							Mathf.Max(dist / Mathf.Max(speed, 0.001f),
									  rotMag / Mathf.Max(MaxAngularSpeed, 0.001f)));
			Vector3 capPos = pos; Vector3 capRot = rotRad; float capDur = dur;
			m_Queue.Enqueue(new Step { OnStart = () => Send(capPos, capRot, capDur), Duration = dur });
			m_PlannedPos = pos; m_PlannedRot = rotRad;
		}

		public void EnqueueOpenGripper (float? s = null)
		{
			Log.Info("EnqueueOpenGripper called.");
			if (m_Gripper == null)
			{
				Log.Critical("Gripper actuator not found.");
			} else
			{
			Log.Info("Gripper actuator found, enqueueing open step.");
				m_Queue.Enqueue(new Step { OnStart = () => { if (m_Gripper != null) m_Gripper.Value = GripperOpenValue; }, Duration = s ?? GripperSettle });
			}
			
		}
		public void EnqueueCloseGripper(float? s = null)
			=> m_Queue.Enqueue(new Step { OnStart = () => { if (m_Gripper != null) m_Gripper.Value = GripperCloseValue; }, Duration = s ?? GripperSettle });
		public void EnqueueWait  (float seconds)
			=> m_Queue.Enqueue(new Step { OnStart = null, Duration = seconds });
		public void EnqueueAction(Action a, float dwell = 0f)
			=> m_Queue.Enqueue(new Step { OnStart = a, Duration = dwell });
		public void EnqueueGoHome()
			=> EnqueueMoveAtSpeed(Home, TopDownRotIdentity, TravelSpeed);

		// Drop any pending steps (e.g. an episode was interrupted/timed out) without touching the robot's current pose.
		public void ClearQueue()
		{
			m_Queue.Clear();
			m_Started = false;
			m_Elapsed = 0f;
		}

		// Routed approach: lift in place → translate at safe altitude → descend.
		// Three SEPARATE queue steps — one SetInputTrigger each.
		public void EnqueueRoutedApproach(Vector3 target, Vector3 rotRad)
		{
			float safeY = Mathf.Max(m_PlannedPos.Y, target.Y + SafeApproachLift);
			EnqueueMoveAtSpeed(new Vector3(m_PlannedPos.X, safeY, m_PlannedPos.Z), rotRad, 0.15f);
			EnqueueMoveAtSpeed(new Vector3(target.X,       safeY, target.Z),       rotRad, TravelSpeed);
			EnqueueMoveAtSpeed(target,                                             rotRad, DescendSpeed);
		}

		// open → routed approach to hover → align → descend → close → retreat
		public void EnqueuePickup(Entity block)
		{
			if (block == null) return;
			Vector3 blockPos = block.Transform.WorldTranslation;
			Vector3 rot      = BuildTopDownGripperRotation(GetVisualYaw(block), GripperYawOffset);
			Vector3 preGrasp = blockPos + new Vector3(0f, PreGraspLiftY + GraspOffsetY, 0f);
			Vector3 graspP   = blockPos + new Vector3(0f, GraspOffsetY, 0f);

			EnqueueOpenGripper(0.15f);
			EnqueueRoutedApproach(preGrasp, rot);
			EnqueueWait(AlignDwell);
			EnqueueMoveAtSpeed(graspP, rot, DescendSpeed);
			EnqueueCloseGripper();
			EnqueueMoveAtSpeed(preGrasp, rot, DescendSpeed);
		}

		// routed approach → align → slow descent → settle → gentle open → retreat.
		// `hover > 0` releases above the slot — use when a same-layer neighbour is already placed.
		public void EnqueuePlacement(Vector3 placePos, float blockYaw, float hover = 0f)
		{
			Vector3 rot      = BuildTopDownGripperRotation(blockYaw, GripperYawOffset);
			Vector3 prePlace = placePos + new Vector3(0f, PreGraspLiftY + GraspOffsetY, 0f);
			Vector3 release  = placePos + new Vector3(0f, hover + GraspOffsetY, 0f);
			EnqueueRoutedApproach(prePlace, rot);
			EnqueueWait(AlignDwell);
			EnqueueMoveAtSpeed(release, rot, PlaceDescendSpeed);
			EnqueueWait(0.3f);   // settle before release
			// Gentle gripper open (5-step ramp avoids the finger-flick that throws the block).
			for (int i = 1; i <= 5; i++)
			{
				float v = GripperCloseValue + (GripperOpenValue - GripperCloseValue) * (i / 5f);
				float vCap = v;
				EnqueueAction(() => { if (m_Gripper != null) m_Gripper.Value = vCap; }, 0.03f);
			}
			EnqueueWait(GripperSettle);
			EnqueueMoveAtSpeed(prePlace, rot, DescendSpeed);
		}

		// ──────────────────────────────────────────────────────────────────
		//  Internals — don't touch unless you really mean to.
		// ──────────────────────────────────────────────────────────────────
		private void Send(Vector3 pos, Vector3 rotRad, float dur)
		{
			if (m_Robot == null) return;
			m_Robot.SetInputVector3(k_Pos, pos);
			m_Robot.SetInputVector3(k_Rot, rotRad * Mathf.Rad2Deg);
			m_Robot.SetInputFloat  (k_Dur, dur);
			m_Robot.SetInputTrigger(k_Go);
		}

		// Top-down rotation (Piper / link6 wrist frame, radians).
		// Pitch +π/2 turns the wrist down; Y carries the yaw that aligns the
		// gripper's closing axis with the block's short edge.
		public static Vector3 BuildTopDownGripperRotation(float blockYawRadians, float yawOffset)
		{
			float yaw = Mathf.WrapToPi(blockYawRadians + yawOffset);
			return new Vector3(Mathf.PIonTwo, yaw, 0f);
		}

		public static float GetVisualYaw(Entity e)
		{
			Vector3 fwd = e.Transform.WorldRotationQuat * new Vector3(0f, 0f, 1f);
			return Mathf.Atan2(fwd.X, fwd.Z);
		}
	}
}
