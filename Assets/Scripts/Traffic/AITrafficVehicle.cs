using System.Collections.Generic;
using UnityEngine;

namespace Nageel.Traffic
{
    public class AITrafficVehicle : MonoBehaviour
    {
        [Header("Movement & Speed")]
        public float baseSpeedKmh = 60f;
        public float currentSpeedKmh = 0f;
        public float minCurveSpeedKmh = 25f;
        public float acceleration = 3.5f;
        public float brakingForce = 7.0f;

        [Header("Lane & Path Following")]
        public float laneOffset = 2.2f; // Positive = Right Lane, Negative = Left Oncoming Lane
        public bool isOncoming = false;
        public float currentDistanceAlongRoad = 100f;
        public float totalPathLength = 10500f;

        [Header("Collision & Awareness")]
        public float safeFollowDistance = 18f;
        public float emergencyStopDistance = 8f;
        public LayerMask obstacleLayers;

        private List<Vector3> splinePoints;
        private Rigidbody rb;
        private bool isBrakingForObstacle = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // Kinematic physics for ultra-efficient mobile AI traffic
            }
        }

        public void Initialize(List<Vector3> path, float startDistance, bool oncoming)
        {
            splinePoints = path;
            currentDistanceAlongRoad = startDistance;
            isOncoming = oncoming;
            laneOffset = oncoming ? -2.2f : 2.2f;
            currentSpeedKmh = baseSpeedKmh * 0.75f;
            UpdateTransformOnPath(0f);
        }

        private void Update()
        {
            if (splinePoints == null || splinePoints.Count < 2) return;

            CheckObstaclesAhead();
            CalculateTargetSpeed();
            MoveVehicle(Time.deltaTime);
        }

        private void CheckObstaclesAhead()
        {
            isBrakingForObstacle = false;
            RaycastHit hit;
            Vector3 rayStart = transform.position + Vector3.up * 0.6f;
            Vector3 rayDir = transform.forward;

            if (Physics.Raycast(rayStart, rayDir, out hit, safeFollowDistance, obstacleLayers))
            {
                isBrakingForObstacle = true;
                if (hit.distance < emergencyStopDistance)
                {
                    currentSpeedKmh = Mathf.Max(0f, currentSpeedKmh - brakingForce * 2.5f * Time.deltaTime * 3.6f);
                }
            }
        }

        private void CalculateTargetSpeed()
        {
            if (isBrakingForObstacle)
            {
                currentSpeedKmh = Mathf.MoveTowards(currentSpeedKmh, 0f, brakingForce * Time.deltaTime * 3.6f);
                return;
            }

            // Estimate upcoming road curvature by sampling points 20m and 45m ahead
            float lookAheadDist = currentDistanceAlongRoad + (isOncoming ? -30f : 30f);
            Vector3 currentPos = GetPositionAtDistance(currentDistanceAlongRoad);
            Vector3 aheadPos = GetPositionAtDistance(lookAheadDist);
            Vector3 farPos = GetPositionAtDistance(lookAheadDist + (isOncoming ? -30f : 30f));

            Vector3 dir1 = (aheadPos - currentPos).normalized;
            Vector3 dir2 = (farPos - aheadPos).normalized;
            float angleTurn = Vector3.Angle(dir1, dir2);

            // If sharp hairpin bend (angle > 18 deg), slow down to minCurveSpeed
            float targetSpeed = baseSpeedKmh;
            if (angleTurn > 12f)
            {
                float turnFactor = Mathf.Clamp01((angleTurn - 12f) / 30f);
                targetSpeed = Mathf.Lerp(baseSpeedKmh, minCurveSpeedKmh, turnFactor);
            }

            // Accelerate or decelerate smoothly
            if (currentSpeedKmh < targetSpeed)
            {
                currentSpeedKmh += acceleration * Time.deltaTime * 3.6f;
            }
            else
            {
                currentSpeedKmh -= brakingForce * Time.deltaTime * 3.6f;
            }

            currentSpeedKmh = Mathf.Clamp(currentSpeedKmh, 0f, baseSpeedKmh);
        }

        private void MoveVehicle(float dt)
        {
            float speedMs = currentSpeedKmh / 3.6f;
            float moveDelta = speedMs * dt;

            if (isOncoming)
            {
                currentDistanceAlongRoad -= moveDelta;
                if (currentDistanceAlongRoad <= 10f)
                {
                    currentDistanceAlongRoad = totalPathLength - 50f;
                }
            }
            else
            {
                currentDistanceAlongRoad += moveDelta;
                if (currentDistanceAlongRoad >= totalPathLength - 50f)
                {
                    currentDistanceAlongRoad = 10f;
                }
            }

            UpdateTransformOnPath(dt);
        }

        private void UpdateTransformOnPath(float dt)
        {
            Vector3 centerPos = GetPositionAtDistance(currentDistanceAlongRoad);
            Vector3 nextPos = GetPositionAtDistance(currentDistanceAlongRoad + (isOncoming ? -3f : 3f));
            Vector3 forward = (nextPos - centerPos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 finalPos = centerPos + right * laneOffset;
            Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);

            transform.position = finalPos;
            transform.rotation = targetRot;
        }

        private Vector3 GetPositionAtDistance(float dist)
        {
            if (splinePoints == null || splinePoints.Count == 0) return Vector3.zero;
            float clampedDist = Mathf.Clamp(dist, 0f, totalPathLength);
            float progress = clampedDist / totalPathLength;
            int idx = Mathf.Clamp(Mathf.FloorToInt(progress * (splinePoints.Count - 1)), 0, splinePoints.Count - 2);
            float localT = (progress * (splinePoints.Count - 1)) - idx;

            return Vector3.Lerp(splinePoints[idx], splinePoints[idx + 1], localT);
        }
    }
}
