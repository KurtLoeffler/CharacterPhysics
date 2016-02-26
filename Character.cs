using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace CharacterPhysics
{
	public class Character:MonoBehaviour
	{
		public class GroundInfo
		{
			public Collider collider;
			public Vector3 position;
			public Vector3 normal;
			public Vector3 velocity;
			public Vector3 angularVelocity;

			private IMovingPlatform _movingPlatform;
			static private List<IMovingPlatform> _componentCache = new List<IMovingPlatform>();
			public IMovingPlatform movingPlatform
			{
				get
				{
					if (_movingPlatform == null)
					{
						_componentCache.Clear();
						collider.gameObject.GetComponentsInParent<IMovingPlatform>(false, _componentCache);
						if (_componentCache.Count > 0)
						{
							_movingPlatform = _componentCache[0];
						}
						
					}
					return _movingPlatform;
				}
			}
		}

		public bool automaticUpdate = true;
		public float footRadiusScaler = 0.75f;
		public float footOffset = 0.2f;
		public float footAnchorRatio = 0.5f;
		public float stepSmoothing = 50.0f;
		public float maxGroundAngle = 50.0f;

		public float groundedDrag = 1.0f;
		public float airLateralDrag = 0.1f;
		
		public GroundInfo groundInfo { get; private set; }

		private bool disableGrounding = false;

		public bool isGrounded
		{
			get
			{
				if (disableGrounding)
				{
					return false;
				}
				return groundInfo != null;
			}
		}
		
		public Vector3 groundVelocity
		{
			get
			{
				if (groundInfo == null)
				{
					return Vector3.zero;
				}

				return CalculateGroundVelocity(groundInfo.movingPlatform);
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
			
			groundInfo = null;

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
					groundInfo = new GroundInfo();
					groundInfo.collider = bestSpherecastHit.collider;
					groundInfo.position = bestSpherecastHit.point;
					groundInfo.normal = bestSpherecastHit.normal;

					if (groundInfo.movingPlatform != null)
					{
						groundInfo.velocity = groundInfo.movingPlatform.GetVelocityAtPoint(groundInfo.position);
					}
					else
					{
						Rigidbody groundRigidbody = groundInfo.collider.gameObject.GetComponentInParent<Rigidbody>();
						if (groundRigidbody)
						{
							groundInfo.velocity = groundRigidbody.GetPointVelocity(groundInfo.position);
							groundInfo.angularVelocity = groundRigidbody.transform.TransformDirection(groundRigidbody.angularVelocity);
						}
					}
				}
			}
			
			if (groundInfo != null)
			{
				Vector3 vel = velocity;
				if (vel.y <= groundInfo.velocity.y)
				{
					Vector3 newPos = characterPos;
					newPos.y = groundInfo.position.y+desiredStandOffset;
					newPos.y = Mathf.Lerp(characterPos.y, newPos.y, deltaTime*stepSmoothing);
					transform.position = newPos;
					disableGrounding = false;
				}
				Debug.DrawLine(groundInfo.position, groundInfo.position+new Vector3(0, footOffset, 0), new Color(0, 0, 1, 1));
			}

			if (isGrounded)
			{
				IMovingPlatform cachedMovingPlatform = groundInfo.movingPlatform;

				Vector3 vel = velocity;
				
				if (cachedMovingPlatform != null && cachedMovingPlatform.sticky)
				{
					vel.y = groundInfo.velocity.y;
				}
				else
				{
					vel.y = Mathf.Max(vel.y, groundInfo.velocity.y);
				}

				float dragFactor = groundedDrag*deltaTime;
				vel.x = Mathf.Lerp(vel.x, groundInfo.velocity.x, (dragFactor));
				vel.z = Mathf.Lerp(vel.z, groundInfo.velocity.z, (dragFactor));

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
