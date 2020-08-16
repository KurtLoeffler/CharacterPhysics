using UnityEngine;
using System.Collections;

namespace CharacterPhysics
{
	public interface IMovingPlatform
	{
		GameObject gameObject { get; }
		bool sticky { get; }
		Vector3 GetVelocityAtPoint(Vector3 point);
	}
}

