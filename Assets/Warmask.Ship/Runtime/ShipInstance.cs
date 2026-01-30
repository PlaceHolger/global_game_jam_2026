using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Warmask.Ship
{
    public class ShipInstance : MonoBehaviour
    {
        [SerializeField]
        private float moveSpeed = 5f;

        [SerializeField]
        private float neighborRadius = 5f;

        [SerializeField]
        private float separationDistance = 2f;

        [SerializeField]
        private float alignmentWeight = 1f;

        [SerializeField]
        private float cohesionWeight = 1f;

        [SerializeField]
        private float separationWeight = 1.5f;

        [SerializeField]
        private float targetWeight = 2f; // Weight for target attraction

        private Vector2 velocity;
        
        [SerializeField]
        private Transform target; // Current target position

        private void Start()
        {
            // Initialize velocity with a random direction and speed
            velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * moveSpeed;
        }

        private void Update()
        {
            Vector2 separation = CalculateSeparation() * separationWeight;
            Vector2 alignment = CalculateAlignment() * alignmentWeight;
            Vector2 cohesion = CalculateCohesion() * cohesionWeight;
            Vector2 targetAttraction = CalculateTargetAttraction() * targetWeight;

            Vector2 desiredDirection = separation + alignment + cohesion + targetAttraction;

            // Ensure desiredDirection is valid
            if (desiredDirection != Vector2.zero)
            {
                desiredDirection.Normalize();

                // Calculate the angle between current velocity and desired direction
                float angle = Vector2.SignedAngle(velocity, desiredDirection);

                // Clamp the angle to the maximum turn angle per second
                float maxTurnAnglePerSecond = 90f; // Maximum turn angle in degrees per second
                float maxTurnAngle = maxTurnAnglePerSecond * Time.deltaTime;
                angle = Mathf.Clamp(angle, -maxTurnAngle, maxTurnAngle);

                // Rotate the velocity vector by the clamped angle
                Quaternion rotation = Quaternion.Euler(0, 0, angle);
                velocity = rotation * velocity;
            }

            // Ensure constant velocity magnitude
            velocity = velocity.normalized * moveSpeed;

            transform.position += (Vector3)(velocity * Time.deltaTime);

            // Check if velocity is not zero before setting transform.up
            if (velocity != Vector2.zero)
            {
                transform.up = velocity.normalized;
            }
        }
        
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private Vector2 CalculateTargetAttraction()
        {
            if (target == null) return Vector2.zero;

            Vector2 directionToTarget = target.position - transform.position;
            return directionToTarget.normalized;
        }

        private Vector2 CalculateSeparation()
        {
            Vector2 separation = Vector2.zero;
            int count = 0;

            foreach (ShipInstance neighbor in GetNeighbors())
            {
                float distance = Vector2.Distance(transform.position, neighbor.transform.position);
                if (distance < separationDistance)
                {
                    separation += (Vector2)(transform.position - neighbor.transform.position).normalized / distance;
                    count++;
                }
            }

            if (count > 0)
            {
                separation /= count;
            }

            return separation;
        }

        private Vector2 CalculateAlignment()
        {
            Vector2 averageVelocity = Vector2.zero;
            int count = 0;

            foreach (ShipInstance neighbor in GetNeighbors())
            {
                averageVelocity += neighbor.velocity;
                count++;
            }

            if (count > 0)
            {
                averageVelocity /= count;
            }

            return averageVelocity.normalized;
        }

        private Vector2 CalculateCohesion()
        {
            Vector2 centerOfMass = Vector2.zero;
            int count = 0;

            foreach (ShipInstance neighbor in GetNeighbors())
            {
                centerOfMass += (Vector2)neighbor.transform.position;
                count++;
            }

            if (count > 0)
            {
                centerOfMass /= count;
                return (centerOfMass - (Vector2)transform.position).normalized;
            }

            return Vector2.zero;
        }

        private List<ShipInstance> GetNeighbors()
        {
            List<ShipInstance> neighbors = new List<ShipInstance>();
            Collider2D[] neighborColliders = Physics2D.OverlapCircleAll(transform.position, neighborRadius);

            foreach (Collider2D neighborCollider in neighborColliders)
            {
                ShipInstance shipInstance = neighborCollider.GetComponent<ShipInstance>();
                if (shipInstance != null && shipInstance != this)
                {
                    neighbors.Add(shipInstance);
                }
            }

            return neighbors;
        }
    }
}