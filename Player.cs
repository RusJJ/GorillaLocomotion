namespace GorillaLocomotion
{
    using UnityEngine;

    public class Player : MonoBehaviour
    {
    // Constants
        public const float flMinRaycastDistance = 0.05f;
        public const float flDefaultPrecision = 0.995f;

    // Private Fields
        private static Player hInstance;
        private Rigidbody hPlayerRigidBody;
        private Vector3 vecLastLeftHandPosition, vecLastRightHandPosition, vecLastHeadPosition;

        // Those are only for saving
        private Vector3[] vecVelHistory;
        private int nCurrentVelIndex;
        private Vector3 vecCurrentVel;
        private Vector3 vecDenormalizedVelAverage;
        private Vector3 vecLastPlayerPosition;

    // Public Fields
        public static Player GetInstance { get { return hInstance; } }
        public SphereCollider hHeadCollider;
        public CapsuleCollider hBodyCollider;
        public Transform hLeftHandFollower, hLeftHandTransform;
        public Transform hRightHandFollower, hRightHandTransform;
        public bool bFreezePlayer = false;

        public int nVelHistorySize; // Should be used in editor
        public float flVelLimit;
        public float flMaxArmLength = 1.0f; // KILL MAN: 1.5 meters is kinda big... TODO: Find a better value
        public float flUnStickDistance = 1.0f;

        public float flMaxJumpSpeed;
        public float flJumpMultiplier;

        public Vector3 vecRightHandOffset;
        public Vector3 vecLeftHandOffset;

        public LayerMask stLocomotionEnabledLayers;

        public bool bWasLeftHandTouching, bWasRightHandTouching;

    // Functions start
        private void Awake()
        {
            if (hInstance != null && hInstance != this)
                Destroy(gameObject);
            else
                hInstance = this;
            InitializeValues();
        }
        
        private void OnSurfaceStartTouch(bool isLeftHand, Vector3 playerVel)
        {
            // Play sound & Haptic feedback/impulse
        }

        private void OnSurfaceEndTouch(bool isLeftHand, Vector3 playerVel)
        {

        }

        public void InitializeValues()
        {
            hPlayerRigidBody = GetComponent<Rigidbody>();
            vecVelHistory = new Vector3[nVelHistorySize];
            nCurrentVelIndex = 0;
            vecLastLeftHandPosition = hLeftHandFollower.transform.position;
            vecLastRightHandPosition = hRightHandFollower.transform.position;
            vecLastHeadPosition = hHeadCollider.transform.position;
            vecLastPlayerPosition = transform.position;
        }

        private Vector3 CurrentLeftHandPosition()
        {
            if ((PositionWithOffset(hLeftHandTransform, vecLeftHandOffset) - hHeadCollider.transform.position).magnitude < flMaxArmLength)
            {
                return PositionWithOffset(hLeftHandTransform, vecLeftHandOffset);
            }
            return hHeadCollider.transform.position + (PositionWithOffset(hLeftHandTransform, vecLeftHandOffset) - hHeadCollider.transform.position).normalized * flMaxArmLength;
        }

        private Vector3 CurrentRightHandPosition()
        {
            if ((PositionWithOffset(hRightHandTransform, vecRightHandOffset) - hHeadCollider.transform.position).magnitude < flMaxArmLength)
            {
                return PositionWithOffset(hRightHandTransform, vecRightHandOffset);
            }
            return hHeadCollider.transform.position + (PositionWithOffset(hRightHandTransform, vecRightHandOffset) - hHeadCollider.transform.position).normalized * flMaxArmLength;
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

            hBodyCollider.transform.eulerAngles = new Vector3(0, hHeadCollider.transform.eulerAngles.y, 0);
            Vector3 timeSaving1 = Vector3.down * 2.0f * 9.81f * Time.fixedDeltaTime * Time.fixedDeltaTime;

            // Left hand

            Vector3 distanceTraveled = CurrentLeftHandPosition() - vecLastLeftHandPosition + timeSaving1;
            if (IterativeCollisionSphereCast(vecLastLeftHandPosition, flMinRaycastDistance, distanceTraveled, flDefaultPrecision, out finalPosition, true))
            {
                // This lets you stick to the position you touch, as long as you keep touching the surface this will be the zero point for that hand
                firstIterationLeftHand = (bWasLeftHandTouching ? vecLastLeftHandPosition : finalPosition) - CurrentLeftHandPosition();
                hPlayerRigidBody.velocity = Vector3.zero;

                leftHandColliding = true;
            }

            // Right hand

            distanceTraveled = CurrentRightHandPosition() - vecLastRightHandPosition + timeSaving1;
            if (IterativeCollisionSphereCast(vecLastRightHandPosition, flMinRaycastDistance, distanceTraveled, flDefaultPrecision, out finalPosition, true))
            {
                firstIterationRightHand = (bWasRightHandTouching ? vecLastRightHandPosition : finalPosition) - CurrentRightHandPosition();
                hPlayerRigidBody.velocity = Vector3.zero;

                rightHandColliding = true;
            }

            // Average or Add

            rigidBodyMovement = firstIterationLeftHand + firstIterationRightHand;
            if ((leftHandColliding || bWasLeftHandTouching) && (rightHandColliding || bWasRightHandTouching))
            {
                // This lets you grab stuff with both hands at the same time
                rigidBodyMovement *= 0.5f;
            }

            // Check valid head movement

            if (IterativeCollisionSphereCast(vecLastHeadPosition, hHeadCollider.radius, hHeadCollider.transform.position + rigidBodyMovement - vecLastHeadPosition, flDefaultPrecision, out finalPosition, false))
            {
                rigidBodyMovement = finalPosition - vecLastHeadPosition;
                // Last check to make sure the head won't phase through geometry
                if (Physics.Raycast(vecLastHeadPosition, hHeadCollider.transform.position - vecLastHeadPosition + rigidBodyMovement, out hitInfo, (hHeadCollider.transform.position - vecLastHeadPosition + rigidBodyMovement).magnitude + hHeadCollider.radius * flDefaultPrecision * 0.999f, stLocomotionEnabledLayers.value))
                {
                    rigidBodyMovement = vecLastHeadPosition - hHeadCollider.transform.position;
                }
            }

            if (rigidBodyMovement != Vector3.zero)
            {
                transform.position = transform.position + rigidBodyMovement;
            }
            vecLastHeadPosition = hHeadCollider.transform.position;

            // Do final left hand position

            distanceTraveled = CurrentLeftHandPosition() - vecLastLeftHandPosition;

            if (IterativeCollisionSphereCast(vecLastLeftHandPosition, flMinRaycastDistance, distanceTraveled, flDefaultPrecision, out finalPosition, !((leftHandColliding || bWasLeftHandTouching) && (rightHandColliding || bWasRightHandTouching))))
            {
                vecLastLeftHandPosition = finalPosition;
                leftHandColliding = true;
                if (!bWasLeftHandTouching) OnSurfaceStartTouch(true, distanceTraveled * Time.fixedDeltaTime);
            }
            else
            {
                vecLastLeftHandPosition = CurrentLeftHandPosition();
            }

            // Do final right hand position

            distanceTraveled = CurrentRightHandPosition() - vecLastRightHandPosition;

            if (IterativeCollisionSphereCast(vecLastRightHandPosition, flMinRaycastDistance, distanceTraveled, flDefaultPrecision, out finalPosition, !((leftHandColliding || bWasLeftHandTouching) && (rightHandColliding || bWasRightHandTouching))))
            {
                vecLastRightHandPosition = finalPosition;
                rightHandColliding = true;
                if (!bWasRightHandTouching) OnSurfaceStartTouch(false, distanceTraveled * Time.fixedDeltaTime);
            }
            else
            {
                vecLastRightHandPosition = CurrentRightHandPosition();
            }

            // More checks?

            StoreVelocities();

            if ((rightHandColliding || leftHandColliding) && !bFreezePlayer)
            {
                if (vecDenormalizedVelAverage.magnitude > flVelLimit)
                {
                    if (vecDenormalizedVelAverage.magnitude * flJumpMultiplier > flMaxJumpSpeed)
                    {
                        hPlayerRigidBody.velocity = vecDenormalizedVelAverage.normalized * flMaxJumpSpeed;
                    }
                    else
                    {
                        hPlayerRigidBody.velocity = flJumpMultiplier * vecDenormalizedVelAverage;
                    }
                }
            }

            // Check to see if left hand is stuck and we should unstick it

            if (leftHandColliding && (CurrentLeftHandPosition() - vecLastLeftHandPosition).magnitude > flUnStickDistance && !Physics.SphereCast(hHeadCollider.transform.position, flMinRaycastDistance * flDefaultPrecision, CurrentLeftHandPosition() - hHeadCollider.transform.position, out hitInfo, (CurrentLeftHandPosition() - hHeadCollider.transform.position).magnitude - flMinRaycastDistance, stLocomotionEnabledLayers.value))
            {
                vecLastLeftHandPosition = CurrentLeftHandPosition();
                leftHandColliding = false;
                OnSurfaceEndTouch(true, distanceTraveled * Time.fixedDeltaTime);
            }

            // Check to see if right hand is stuck and we should unstick it

            if (rightHandColliding && (CurrentRightHandPosition() - vecLastRightHandPosition).magnitude > flUnStickDistance && !Physics.SphereCast(hHeadCollider.transform.position, flMinRaycastDistance * flDefaultPrecision, CurrentRightHandPosition() - hHeadCollider.transform.position, out hitInfo, (CurrentRightHandPosition() - hHeadCollider.transform.position).magnitude - flMinRaycastDistance, stLocomotionEnabledLayers.value))
            {
                vecLastRightHandPosition = CurrentRightHandPosition();
                rightHandColliding = false;
                OnSurfaceEndTouch(false, distanceTraveled * Time.fixedDeltaTime);
            }

            // KILL MAN: idk if need this exactly
            if(bWasLeftHandTouching && !leftHandColliding)
                OnSurfaceEndTouch(true, distanceTraveled * Time.fixedDeltaTime);
            else if(bWasRightHandTouching && !rightHandColliding)
                OnSurfaceEndTouch(false, distanceTraveled * Time.fixedDeltaTime);

            hLeftHandFollower.position = vecLastLeftHandPosition;
            hRightHandFollower.position = vecLastRightHandPosition;

            bWasLeftHandTouching = leftHandColliding;
            bWasRightHandTouching = rightHandColliding;
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
                slipPercentage = gorillaSurface != null ? gorillaSurface.flSlipPercentage : (!singleHand ? Surface.flDefaultSlipPercentage : 0.001f);
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
            }
            else
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
            if (Physics.SphereCast(startPosition, sphereRadius * precision, movementVector, out hitInfo, movementVector.magnitude + sphereRadius * (1 - precision), stLocomotionEnabledLayers.value))
            {
                // If we hit, we're trying to move to a position a sphereradius distance from the normal
                finalPosition = hitInfo.point + hitInfo.normal * sphereRadius;

                // Check a spherecase from the original position to the intended final position
                if (Physics.SphereCast(startPosition, sphereRadius * precision * precision, finalPosition - startPosition, out innerHit, (finalPosition - startPosition).magnitude + sphereRadius * (1 - precision * precision), stLocomotionEnabledLayers.value))
                {
                    finalPosition = startPosition + (finalPosition - startPosition).normalized * Mathf.Max(0, hitInfo.distance - sphereRadius * (1f - precision * precision));
                    hitInfo = innerHit;
                }
                // Bonus raycast check to make sure that something odd didn't happen with the spherecast. helps prevent clipping through geometry
                else if (Physics.Raycast(startPosition, finalPosition - startPosition, out innerHit, (finalPosition - startPosition).magnitude + sphereRadius * precision * precision * 0.999f, stLocomotionEnabledLayers.value))
                {
                    finalPosition = startPosition;
                    hitInfo = innerHit;
                    return true;
                }
                return true;
            }
            // Anti-clipping through geometry check
            // KILL MAN: TODO: Probably not enough
            else if (Physics.Raycast(startPosition, movementVector, out hitInfo, movementVector.magnitude + sphereRadius * precision * 0.999f, stLocomotionEnabledLayers.value))
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
            return checkLeftHand ? bWasLeftHandTouching : bWasRightHandTouching;
        }

        public void Turn(float degrees)
        {
            Quaternion tmp = Quaternion.Euler(0, degrees, 0); // KILL MAN: Do not calculate it that often..?
            transform.RotateAround(hHeadCollider.transform.position, transform.up, degrees);
            vecDenormalizedVelAverage = tmp * vecDenormalizedVelAverage;
            for (int i = 0; i < vecVelHistory.Length; ++i)
            {
                vecVelHistory[i] = tmp * vecVelHistory[i];
            }
        }

        private void StoreVelocities()
        {
            nCurrentVelIndex = (nCurrentVelIndex + 1) % nVelHistorySize;
            vecCurrentVel = (transform.position - vecLastPlayerPosition) / Time.fixedDeltaTime;
            vecDenormalizedVelAverage += (vecCurrentVel - vecVelHistory[nCurrentVelIndex]) / (float)nVelHistorySize;
            vecVelHistory[nCurrentVelIndex] = vecCurrentVel;
            vecLastPlayerPosition = transform.position;
        }
    }
}