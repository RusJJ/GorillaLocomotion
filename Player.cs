namespace GorillaLocomotion
{
    using UnityEngine;

    public class Player : MonoBehaviour
    {
    // Constants
        public const float m_flMinRaycastDistance = 0.05f;
        public const float m_flDefaultPrecision = 0.995f;

    // Private Fields
        private static Player m_hInstance;
        private Rigidbody m_hPlayerRigidBody;
        private Vector3 m_vecLastLeftHandPosition, m_vecLastRightHandPosition, m_vecLastHeadPosition;

        // Those are only for saving
        private Vector3[] m_vecVelHistory;
        private int m_nCurrentVelIndex;
        private Vector3 m_vecCurrentVel;
        private Vector3 m_vecDenormalizedVelAverage;
        private Vector3 m_vecLastPlayerPosition;

    // Public Fields
        public static Player GetInstance { get { return m_hInstance; } }
        public SphereCollider m_hHeadCollider;
        public CapsuleCollider m_hBodyCollider;
        public Transform m_hLeftHandFollower, m_hLeftHandTransform;
        public Transform m_hRightHandFollower, m_hRightHandTransform;
        public bool m_bFreezePlayer = false;

        public int m_nVelHistorySize; // Should be used in editor
        public float m_flVelLimit;
        public float m_flMaxArmLength = 1.0f; // KILL MAN: 1.5 meters is kinda big... TODO: Find a better value
        public float m_flUnStickDistance = 1.0f;

        public float m_flMaxJumpSpeed;
        public float m_flJumpMultiplier;

        public Vector3 m_vecRightHandOffset;
        public Vector3 m_vecLeftHandOffset;

        public LayerMask m_stLocomotionEnabledLayers;

        public bool m_bWasLeftHandTouching, m_bWasRightHandTouching;

    // Functions start
        private void Awake()
        {
            if (m_hInstance != null && m_hInstance != this)
                Destroy(gameObject);
            else
                m_hInstance = this;
            InitializeValues();
        }

        public void InitializeValues()
        {
            m_hPlayerRigidBody = GetComponent<Rigidbody>();
            m_vecVelHistory = new Vector3[m_nVelHistorySize];
            m_nCurrentVelIndex = 0;
            m_vecLastLeftHandPosition = m_hLeftHandFollower.transform.position;
            m_vecLastRightHandPosition = m_hRightHandFollower.transform.position;
            m_vecLastHeadPosition = m_hHeadCollider.transform.position;
            m_vecLastPlayerPosition = transform.position;
        }

        private Vector3 CurrentLeftHandPosition()
        {
            if ((PositionWithOffset(m_hLeftHandTransform, m_vecLeftHandOffset) - m_hHeadCollider.transform.position).magnitude < m_flMaxArmLength)
            {
                return PositionWithOffset(m_hLeftHandTransform, m_vecLeftHandOffset);
            }
            return m_hHeadCollider.transform.position + (PositionWithOffset(m_hLeftHandTransform, m_vecLeftHandOffset) - m_hHeadCollider.transform.position).normalized * m_flMaxArmLength;
        }

        private Vector3 CurrentRightHandPosition()
        {
            if ((PositionWithOffset(m_hRightHandTransform, m_vecRightHandOffset) - m_hHeadCollider.transform.position).magnitude < m_flMaxArmLength)
            {
                return PositionWithOffset(m_hRightHandTransform, m_vecRightHandOffset);
            }
            return m_hHeadCollider.transform.position + (PositionWithOffset(m_hRightHandTransform, m_vecRightHandOffset) - m_hHeadCollider.transform.position).normalized * m_flMaxArmLength;
        }

        private Vector3 PositionWithOffset(Transform transformToModify, Vector3 offsetVector)
        {
            return transformToModify.position + transformToModify.rotation * offsetVector;
        }

        // KILL MAN: Probably needs to be a FixedUpdate
        private void FixedUpdate()
        {
            bool leftHandColliding = false;
            bool rightHandColliding = false;
            Vector3 finalPosition;
            Vector3 firstIterationLeftHand = Vector3.zero;
            Vector3 firstIterationRightHand = Vector3.zero;
            Vector3 rigidBodyMovement = Vector3.zero;
            RaycastHit hitInfo;

            m_hBodyCollider.transform.eulerAngles = new Vector3(0, m_hHeadCollider.transform.eulerAngles.y, 0);
            Vector3 timeSaving1 = Vector3.down * 2.0f * 9.81f * Time.deltaTime * Time.deltaTime;

            // Left hand

            Vector3 distanceTraveled = CurrentLeftHandPosition() - m_vecLastLeftHandPosition + timeSaving1;
            if (IterativeCollisionSphereCast(m_vecLastLeftHandPosition, m_flMinRaycastDistance, distanceTraveled, m_flDefaultPrecision, out finalPosition, true))
            {
                // This lets you stick to the position you touch, as long as you keep touching the surface this will be the zero point for that hand
                firstIterationLeftHand = (m_bWasLeftHandTouching ? m_vecLastLeftHandPosition : finalPosition) - CurrentLeftHandPosition();
                m_hPlayerRigidBody.velocity = Vector3.zero;

                leftHandColliding = true;
            }

            // Right hand

            distanceTraveled = CurrentRightHandPosition() - m_vecLastRightHandPosition + timeSaving1;
            if (IterativeCollisionSphereCast(m_vecLastRightHandPosition, m_flMinRaycastDistance, distanceTraveled, m_flDefaultPrecision, out finalPosition, true))
            {
                firstIterationRightHand = (m_bWasRightHandTouching ? m_vecLastRightHandPosition : finalPosition) - CurrentRightHandPosition();
                m_hPlayerRigidBody.velocity = Vector3.zero;

                rightHandColliding = true;
            }

            // Average or Add

            rigidBodyMovement = firstIterationLeftHand + firstIterationRightHand;
            if ((leftHandColliding || m_bWasLeftHandTouching) && (rightHandColliding || m_bWasRightHandTouching))
            {
                // This lets you grab stuff with both hands at the same time
                rigidBodyMovement *= 0.5f;
            }

            // Check valid head movement

            if (IterativeCollisionSphereCast(m_vecLastHeadPosition, m_hHeadCollider.radius, m_hHeadCollider.transform.position + rigidBodyMovement - m_vecLastHeadPosition, m_flDefaultPrecision, out finalPosition, false))
            {
                rigidBodyMovement = finalPosition - m_vecLastHeadPosition;
                // Last check to make sure the head won't phase through geometry
                if (Physics.Raycast(m_vecLastHeadPosition, m_hHeadCollider.transform.position - m_vecLastHeadPosition + rigidBodyMovement, out hitInfo, (m_hHeadCollider.transform.position - m_vecLastHeadPosition + rigidBodyMovement).magnitude + m_hHeadCollider.radius * m_flDefaultPrecision * 0.999f, m_stLocomotionEnabledLayers.value))
                {
                    rigidBodyMovement = m_vecLastHeadPosition - m_hHeadCollider.transform.position;
                }
            }

            if (rigidBodyMovement != Vector3.zero)
            {
                transform.position = transform.position + rigidBodyMovement;
            }
            m_vecLastHeadPosition = m_hHeadCollider.transform.position;

            // Do final left hand position

            distanceTraveled = CurrentLeftHandPosition() - m_vecLastLeftHandPosition;

            if (IterativeCollisionSphereCast(m_vecLastLeftHandPosition, m_flMinRaycastDistance, distanceTraveled, m_flDefaultPrecision, out finalPosition, !((leftHandColliding || m_bWasLeftHandTouching) && (rightHandColliding || m_bWasRightHandTouching))))
            {
                m_vecLastLeftHandPosition = finalPosition;
                leftHandColliding = true;
            }
            else
            {
                m_vecLastLeftHandPosition = CurrentLeftHandPosition();
            }

            // Do final right hand position

            distanceTraveled = CurrentRightHandPosition() - m_vecLastRightHandPosition;

            if (IterativeCollisionSphereCast(m_vecLastRightHandPosition, m_flMinRaycastDistance, distanceTraveled, m_flDefaultPrecision, out finalPosition, !((leftHandColliding || m_bWasLeftHandTouching) && (rightHandColliding || m_bWasRightHandTouching))))
            {
                m_vecLastRightHandPosition = finalPosition;
                rightHandColliding = true;
            }
            else
            {
                m_vecLastRightHandPosition = CurrentRightHandPosition();
            }

            // More checks?

            StoreVelocities();

            if ((rightHandColliding || leftHandColliding) && !m_bFreezePlayer)
            {
                if (m_vecDenormalizedVelAverage.magnitude > m_flVelLimit)
                {
                    if (m_vecDenormalizedVelAverage.magnitude * m_flJumpMultiplier > m_flMaxJumpSpeed)
                    {
                        m_hPlayerRigidBody.velocity = m_vecDenormalizedVelAverage.normalized * m_flMaxJumpSpeed;
                    }
                    else
                    {
                        m_hPlayerRigidBody.velocity = m_flJumpMultiplier * m_vecDenormalizedVelAverage;
                    }
                }
            }

            // Check to see if left hand is stuck and we should unstick it

            if (leftHandColliding && (CurrentLeftHandPosition() - m_vecLastLeftHandPosition).magnitude > m_flUnStickDistance && !Physics.SphereCast(m_hHeadCollider.transform.position, m_flMinRaycastDistance * m_flDefaultPrecision, CurrentLeftHandPosition() - m_hHeadCollider.transform.position, out hitInfo, (CurrentLeftHandPosition() - m_hHeadCollider.transform.position).magnitude - m_flMinRaycastDistance, m_stLocomotionEnabledLayers.value))
            {
                m_vecLastLeftHandPosition = CurrentLeftHandPosition();
                leftHandColliding = false;
            }

            // Check to see if right hand is stuck and we should unstick it

            if (rightHandColliding && (CurrentRightHandPosition() - m_vecLastRightHandPosition).magnitude > m_flUnStickDistance && !Physics.SphereCast(m_hHeadCollider.transform.position, m_flMinRaycastDistance * m_flDefaultPrecision, CurrentRightHandPosition() - m_hHeadCollider.transform.position, out hitInfo, (CurrentRightHandPosition() - m_hHeadCollider.transform.position).magnitude - m_flMinRaycastDistance, m_stLocomotionEnabledLayers.value))
            {
                m_vecLastRightHandPosition = CurrentRightHandPosition();
                rightHandColliding = false;
            }

            m_hLeftHandFollower.position = m_vecLastLeftHandPosition;
            m_hRightHandFollower.position = m_vecLastRightHandPosition;

            m_bWasLeftHandTouching = leftHandColliding;
            m_bWasRightHandTouching = rightHandColliding;
        }

        private bool IterativeCollisionSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, float precision, out Vector3 endPosition, bool singleHand)
        {
            RaycastHit hitInfo;
            Vector3 movementToProjectedAboveCollisionPlane;
            Surface gorillaSurface;
            float slipPercentage;
            // First spherecast from the starting position to the final position
            if (CollisionsSphereCast(startPosition, sphereRadius * precision, movementVector, precision, out endPosition, out hitInfo))
            {
                // If we hit a surface, do a bit of a slide. this makes it so if you grab with two hands you don't stick 100%, and if you're pushing along a surface while braced with your head, your hand will slide a bit

                // Take the surface normal that we hit, then along that plane, do a spherecast to a position a small distance away to account for moving perpendicular to that surface
                Vector3 firstPosition = endPosition;
                gorillaSurface = hitInfo.collider.GetComponent<Surface>();
                slipPercentage = gorillaSurface != null ? gorillaSurface.m_flSlipPercentage : (!singleHand ? Surface.m_flDefaultSlipPercentage : 0.001f);
                movementToProjectedAboveCollisionPlane = Vector3.ProjectOnPlane(startPosition + movementVector - firstPosition, hitInfo.normal) * slipPercentage;
                if (CollisionsSphereCast(endPosition, sphereRadius, movementToProjectedAboveCollisionPlane, precision * precision, out endPosition, out hitInfo))
                {
                    // If we hit trying to move perpendicularly, stop there and our end position is the final spot we hit
                    return true;
                }
                // If not, try to move closer towards the true point to account for the fact that the movement along the normal of the hit could have moved you away from the surface
                else if (CollisionsSphereCast(movementToProjectedAboveCollisionPlane + firstPosition, sphereRadius, startPosition + movementVector - (movementToProjectedAboveCollisionPlane + firstPosition), precision * precision * precision, out endPosition, out hitInfo))
                {
                    // If we hit, then return the spot we hit
                    return true;
                }
                else
                {
                    // This shouldn't really happen, since this means that the sliding motion got you around some corner or something and let you get to your final point. back off because something strange happened, so just don't do the slide
                    endPosition = firstPosition;
                    return true;
                }
            }
            // As kind of a sanity check, try a smaller spherecast. This accounts for times when the original spherecast was already touching a surface so it didn't trigger correctly
            else if (CollisionsSphereCast(startPosition, sphereRadius * precision * 0.66f, movementVector.normalized * (movementVector.magnitude + sphereRadius * precision * 0.34f), precision * 0.66f, out endPosition, out hitInfo))
            {
                endPosition = startPosition;
                return true;
            } else
            {
                endPosition = Vector3.zero;
                return false;
            }
        }

        private bool CollisionsSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, float precision, out Vector3 finalPosition, out RaycastHit hitInfo)
        {
            // Kind of like a souped up spherecast. Includes checks to make sure that the sphere we're using, if it touches a surface, is pushed away the correct distance (the original sphereradius distance). Since you might
            // be pushing into sharp corners, this might not always be valid, so that's what the extra checks are for

            // Initial spherecase
            RaycastHit innerHit;
            if (Physics.SphereCast(startPosition, sphereRadius * precision, movementVector, out hitInfo, movementVector.magnitude + sphereRadius * (1 - precision), m_stLocomotionEnabledLayers.value))
            {
                // If we hit, we're trying to move to a position a sphereradius distance from the normal
                finalPosition = hitInfo.point + hitInfo.normal * sphereRadius;

                // Check a spherecase from the original position to the intended final position
                if (Physics.SphereCast(startPosition, sphereRadius * precision * precision, finalPosition - startPosition, out innerHit, (finalPosition - startPosition).magnitude + sphereRadius * (1 - precision * precision), m_stLocomotionEnabledLayers.value))
                {
                    finalPosition = startPosition + (finalPosition - startPosition).normalized * Mathf.Max(0, hitInfo.distance - sphereRadius * (1f - precision * precision));
                    hitInfo = innerHit;
                }
                // Bonus raycast check to make sure that something odd didn't happen with the spherecast. helps prevent clipping through geometry
                else if (Physics.Raycast(startPosition, finalPosition - startPosition, out innerHit, (finalPosition - startPosition).magnitude + sphereRadius * precision * precision * 0.999f, m_stLocomotionEnabledLayers.value))
                {
                    finalPosition = startPosition;
                    hitInfo = innerHit;
                    return true;
                }
                return true;
            }
            // Anti-clipping through geometry check
            // KILL MAN: TODO: Probably not enough
            else if (Physics.Raycast(startPosition, movementVector, out hitInfo, movementVector.magnitude + sphereRadius * precision * 0.999f, m_stLocomotionEnabledLayers.value))
            {
                finalPosition = startPosition;
                return true;
            }
            else
            {
                finalPosition = Vector3.zero;
                return false;
            }
        }

        public bool IsHandTouching(bool checkLeftHand)
        {
            return checkLeftHand ? m_bWasLeftHandTouching : m_bWasRightHandTouching;
        }

        public void Turn(float degrees)
        {
            Quaternion tmp = Quaternion.Euler(0, degrees, 0); // KILL MAN: Do not calculate it that often..?
            transform.RotateAround(m_hHeadCollider.transform.position, transform.up, degrees);
            m_vecDenormalizedVelAverage = tmp * m_vecDenormalizedVelAverage;
            for (int i = 0; i < m_vecVelHistory.Length; ++i)
            {
                m_vecVelHistory[i] = tmp * m_vecVelHistory[i];
            }
        }

        private void StoreVelocities()
        {
            m_nCurrentVelIndex = (m_nCurrentVelIndex + 1) % m_nVelHistorySize;
            m_vecCurrentVel = (transform.position - m_vecLastPlayerPosition) / Time.deltaTime;
            m_vecDenormalizedVelAverage += (m_vecCurrentVel - m_vecVelHistory[m_nCurrentVelIndex]) / (float)m_nVelHistorySize;
            m_vecVelHistory[m_nCurrentVelIndex] = m_vecCurrentVel;
            m_vecLastPlayerPosition = transform.position;
        }
    }
}