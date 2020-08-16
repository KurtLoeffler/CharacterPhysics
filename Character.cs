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

		public LayerMask standLayerMask = -1;
		public bool automaticUpdate = true;
		[Range(0, 1)]
		public float footRadiusScaler = 0.75f;
		public float footOffset = 0.2f;
		[Range(0, 1)]
		public float footAnchorRatio = 0.5f;
		public float stepSmoothing = 10;
		[Range(0, 1)]
		public float stepSmoothingPullFactor = 1;
		[Range(0, 90)]
		public float maxGroundAngle = 50.0f;
		[Range(0, 90)]
		public float minSlideAngle = 0;
		[Range(0, 90)]
		public float maxSlideAngle = 0;

		public float groundedDrag = 0.75f;
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

		public float bottomFootOffset
		{
			get
			{
				return (height*0.5f)+footOffset;
			}
		}

		public float standOffset
		{
			get
			{
				return bottomFootOffset-footOffset*(1.0f-footAnchorRatio);
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

			float cachedBottomFootOffset = bottomFootOffset;
			float cachedStandOffset = standOffset;

			Vector3 cPoint1 = transform.position+new Vector3(0, 0, 0);
			float shpherecastDistance = cachedBottomFootOffset;

			hits = Physics.SphereCastAll(cPoint1, footRadius, -transform.up, shpherecastDistance, standLayerMask, QueryTriggerInteraction.Ignore);
			groundInfo = null;

			if (hits.Length > 0)
			{
				RaycastHit bestSpherecastHit = new RaycastHit();
				float bestLocalHitY = Mathf.Infinity;
				for (int i = 0; i < hits.Length; i++)
				{
					RaycastHit hit = hits[i];

					if (hit.collider.isTrigger || hit.collider == capsuleCollider)
					{
						continue;
					}

					if (Vector3.Dot(hit.normal, transform.up) <= 0)
					{
						continue;
					}

					Vector3 localHitPoint = transform.InverseTransformPoint(hit.point);
					if (localHitPoint.y < -cachedBottomFootOffset)
					{
						continue;
					}

					if (!bestSpherecastHit.collider || localHitPoint.y > bestLocalHitY)
					{
						bestSpherecastHit = hit;
						bestLocalHitY = localHitPoint.y;
					}
				}

				if (bestSpherecastHit.collider)
				{
					var epsilon = cachedBottomFootOffset*0.001f;
					var ray = new Ray(bestSpherecastHit.point-bestSpherecastHit.normal*epsilon+transform.up*epsilon*2, -transform.up);
					RaycastHit raycastHit;
					if (bestSpherecastHit.collider.Raycast(ray, out raycastHit, shpherecastDistance))
					{
						bestSpherecastHit = raycastHit;
					}

					if (Vector3.Angle(bestSpherecastHit.normal, transform.up) > maxGroundAngle)
					{
						bestSpherecastHit = default;
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
				Vector3 vel = transform.InverseTransformDirection(velocity);
				Vector3 groundVel = transform.InverseTransformDirection(groundInfo.velocity);
				if (vel.y <= groundVel.y)
				{
					Vector3 newPos = Vector3.zero;
					Vector3 localGroundPos = transform.InverseTransformPoint(groundInfo.position);
					newPos.y = localGroundPos.y+cachedStandOffset;

					float actualStepSmoothing = stepSmoothing;
					if (newPos.y < 0)
					{
						actualStepSmoothing *= stepSmoothingPullFactor;
						float groundDot = Vector3.Dot(transform.up, groundInfo.normal);
						actualStepSmoothing = Mathf.Lerp(0, actualStepSmoothing, groundDot);
					}
					newPos.y = Mathf.Lerp(0, newPos.y, 1-Mathf.Exp(-actualStepSmoothing*deltaTime));
					
					transform.position = transform.TransformPoint(newPos);
					rigidbody.position = transform.position;
					disableGrounding = false;
				}

				Debug.DrawLine(groundInfo.position, groundInfo.position+transform.up*footOffset, new Color(0, 0, 1, 1));
			}

			if (isGrounded)
			{
				IMovingPlatform cachedMovingPlatform = groundInfo.movingPlatform;

				var vel = transform.InverseTransformDirection(velocity);
				var localGroundVelocity = transform.InverseTransformDirection(groundInfo.velocity);
				var localGroundNormal = transform.InverseTransformDirection(groundInfo.normal);

				vel.y -= localGroundVelocity.y;

				if (Vector3.Dot(localGroundNormal, vel.normalized) < 0 || (cachedMovingPlatform != null && cachedMovingPlatform.sticky))
				{
					var flatVector = Vector3.ProjectOnPlane(vel, Vector3.up);
					float flatMag = flatVector.magnitude;

					vel = Vector3.ProjectOnPlane(vel, localGroundNormal);

					float minSlide = minSlideAngle/360;
					float maxSlide = maxSlideAngle/360;
					float slideLength = maxSlide-minSlide;
					{
						float angle = (1-Vector3.Dot(localGroundNormal, Vector3.up));
						float slideFactor = 0;
						if (angle > minSlide)
						{
							slideFactor = 1;
							if (slideLength > 0)
							{
								slideFactor = (angle-minSlide)/(slideLength);
							}
						}
						vel = Vector3.Lerp(flatVector, vel, slideFactor);
					}

					var newFlatVector = Vector3.ProjectOnPlane(vel, Vector3.up);
					newFlatVector = newFlatVector.normalized* Mathf.Max(newFlatVector.magnitude, flatMag);
					vel.x = newFlatVector.x;
					vel.z = newFlatVector.z;
				}

				vel.y += localGroundVelocity.y;

				float dragFactor = 1-Mathf.Exp(-groundedDrag*deltaTime);
				vel.x = Mathf.Lerp(vel.x, localGroundVelocity.x, dragFactor);
				vel.z = Mathf.Lerp(vel.z, localGroundVelocity.z, dragFactor);
				velocity = transform.TransformDirection(vel);
			}
			else
			{
				var vel = transform.InverseTransformDirection(velocity);
				vel.x /= 1.0f+(airLateralDrag*deltaTime);
				vel.z /= 1.0f+(airLateralDrag*deltaTime);
				velocity = transform.TransformDirection(vel);
			}
		}

		public void Unground()
		{
			disableGrounding = true;
		}
	}
}
