using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace CharacterPhysics
{
	public class Character:MonoBehaviour
	{
		public bool automaticUpdate = true;
		public float footRadiusScaler = 0.75f;
		public float footOffset = 0.2f;
		public float footAnchorRatio = 0.5f;
		public float stepSmoothing = 50.0f;
		public float maxGroundAngle = 50.0f;

		public float groundedDrag = 1.0f;
		public float airLateralDrag = 0.1f;

		public Vector3 groundPos { get; private set; }
		public Vector3 groundNormal { get; private set; }
		public Vector3 footNormal { get; private set; }
		public GameObject groundObject { get; private set; }

		private bool disableGrounding = false;

		public bool isGrounded
		{
			get
			{
				if (disableGrounding)
				{
					return false;
				}
				return groundObject;
			}
		}

		private List<IMovingPlatform> _componentCache = new List<IMovingPlatform>();
		public IMovingPlatform movingPlatform
		{
			get
			{
				if (!groundObject)
				{
					return null;
				}
				_componentCache.Clear();
				groundObject.GetComponentsInParent<IMovingPlatform>(false, _componentCache);
				if (_componentCache.Count > 0)
				{
					return _componentCache[0];
				}
				return null;
			}
		}

		public Vector3 groundVelocity
		{
			get
			{
				return CalculateGroundVelocity(movingPlatform);
			}
		}

		private Vector3 CalculateGroundVelocity(IMovingPlatform mp)
		{
			if (mp != null)
			{
				Vector3 localGroundPosition = mp.gameObject.transform.InverseTransformPoint(transform.position);
				return mp.GetVelocityAtPoint(localGroundPosition);
			}
			else
			{
				return Vector3.zero;
			}
		}

		private Rigidbody _rigidbody;
		new public Rigidbody rigidbody
		{
			get
			{
				if (!_rigidbody)
				{
					_rigidbody = gameObject.GetComponent<Rigidbody>();
				}
				return _rigidbody;
			}
		}

		private CapsuleCollider _capsuleCollider;
		public CapsuleCollider capsuleCollider
		{
			get
			{
				if (!_capsuleCollider)
				{
					_capsuleCollider = gameObject.GetComponent<CapsuleCollider>();
				}
				return _capsuleCollider;
			}
		}

		private Vector3 characterControllerVelocity;
		public Vector3 velocity
		{
			get
			{
				return rigidbody.velocity;
			}
			set
			{
				rigidbody.velocity = value;
			}
		}

		public float height
		{
			get
			{
				return capsuleCollider.height;
			}
		}

		public float radius
		{
			get
			{
				return capsuleCollider.radius;
			}
		}

		void Start()
		{

		}

		void FixedUpdate()
		{
			if (automaticUpdate)
			{
				UpdateMotion(Time.fixedDeltaTime);
			}
		}

		public void UpdateMotion(float deltaTime)
		{
			RaycastHit[] hits;

			float footRadius = radius*footRadiusScaler;

			float bottomFootOffset = (height*0.5f)+footOffset;
			float desiredStandOffset = bottomFootOffset-footOffset*(1.0f-footAnchorRatio);

			Vector3 cPoint1 = transform.position+new Vector3(0, 0, 0);
			float shpherecastDistance = bottomFootOffset;
			hits = Physics.SphereCastAll(cPoint1, footRadius, new Vector3(0, -1, 0), shpherecastDistance);
			//Vector3 shperecastEndPoint = cPoint1+(new Vector3(0,-1,0)*shpherecastDistance);
			//Debug.DrawLine(cPoint1,shperecastEndPoint,new Color(1,1,0,1));
			//Debug.DrawLine(cPoint1+(new Vector3(0,-1,0)*shpherecastDistance),shperecastEndPoint+new Vector3(footRadius,0,0),new Color(1,0,0,1));
			//Debug.DrawLine(cPoint1+(new Vector3(0,-1,0)*shpherecastDistance),shperecastEndPoint+new Vector3(0,-footRadius,0),new Color(0,1,0,1));

			groundPos = Vector3.zero;
			groundNormal = Vector3.zero;
			footNormal = Vector3.zero;
			groundObject = null;

			Vector3 characterPos = transform.position;
			if (hits.Length > 0)
			{
				RaycastHit bestSpherecastHit = new RaycastHit();

				for (int i = 0; i < hits.Length; i++)
				{
					RaycastHit hit = hits[i];

					if (hit.collider.isTrigger || hit.collider == capsuleCollider)
					{
						continue;
					}

					if (Vector3.Angle(hit.normal, Vector3.up) > maxGroundAngle)
					{
						continue;
					}

					Vector3 localHitPoint = transform.InverseTransformPoint(hit.point);
					if (localHitPoint.y < -bottomFootOffset)
					{
						continue;
					}

					if (!bestSpherecastHit.collider || hit.distance < bestSpherecastHit.distance)
					{
						bestSpherecastHit = hit;
					}
				}

				if (bestSpherecastHit.collider)
				{
					groundPos = bestSpherecastHit.point;
					groundNormal = bestSpherecastHit.normal;
					footNormal = bestSpherecastHit.normal;
					groundObject = bestSpherecastHit.collider.gameObject;
				}
			}

			IMovingPlatform cachedMovingPlatform = null;
			Vector3 cachedGroundVelocity = Vector3.zero;

			if (groundObject)
			{
				cachedMovingPlatform = movingPlatform;
				if (cachedMovingPlatform != null)
				{
					cachedGroundVelocity = CalculateGroundVelocity(cachedMovingPlatform);
				}
			}

			if (groundObject)
			{

				Vector3 vel = velocity;
				if (vel.y <= cachedGroundVelocity.y)
				{
					Vector3 newPos = characterPos;
					newPos.y = groundPos.y+desiredStandOffset;
					newPos.y = Mathf.Lerp(characterPos.y, newPos.y, deltaTime*stepSmoothing);
					transform.position = newPos;
					disableGrounding = false;
				}
				Debug.DrawLine(groundPos, groundPos+new Vector3(0, footOffset, 0), new Color(0, 0, 1, 1));
			}

			if (isGrounded)
			{
				Vector3 vel = velocity;

				Vector3 gv = cachedGroundVelocity;

				if (cachedMovingPlatform != null && cachedMovingPlatform.sticky)
				{
					vel.y = gv.y;
				}
				else
				{
					vel.y = Mathf.Max(vel.y, gv.y);
				}
				vel.x = Mathf.Lerp(vel.x, gv.x, (groundedDrag*deltaTime));
				vel.z = Mathf.Lerp(vel.z, gv.z, (groundedDrag*deltaTime));

				velocity = vel;
			}
			else
			{
				Vector3 vel = velocity;
				vel.x /= 1.0f+(airLateralDrag*deltaTime);
				vel.z /= 1.0f+(airLateralDrag*deltaTime);
				velocity = vel;
			}
		}

		public void Unground()
		{
			disableGrounding = true;
		}
	}
}
