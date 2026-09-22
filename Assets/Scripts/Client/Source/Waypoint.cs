using System;
using Hazel;

namespace LuckyInterview
{
	public class Waypoint : Entity
	{
		[Group("Waypoint Flag")]
		[Dropdown("start_left", "start_right", "neutral_left", "neutral_right", "insert", "neutral_center")]
		public int TypeIndex = 5;
	    public WaypointFlag waypointFlag => (WaypointFlag)TypeIndex;

		public Transform GetTransform()
		{
			// Todo- sanity check
			return GetComponent<TransformComponent>()!.WorldTransform;
		}


	}

	
	public enum WaypointFlag {
	    start_left,
	    start_right,
	    neutral_left,
		neutral_right,
	    insert,
		neutral_center
	}
}
	