using UnityEngine;
using UnityEngine.AI;

namespace MediaPipeTest.CRT.Surveillance
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class HospitalWanderer : MonoBehaviour
    {
        public Animator animator;
        public Bounds roamingBounds;
        public Vector2 idleSeconds = new(1, 3);
        public int Arrivals { get; private set; }
        public int Destinations { get; private set; }
        public int StuckRecoveries { get; private set; }
        public bool Waiting { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        float waitUntil, progressTime;
        Vector3 progressPosition;
        NavMeshPath path;
        void Awake() { path = new NavMeshPath(); Agent = GetComponent<NavMeshAgent>(); if (animator) animator.applyRootMotion = false; }
        void Start() { Agent.enabled = true; Waiting = true; waitUntil = Time.time + 1; }
        void Update()
        {
            if (animator) animator.SetFloat("Speed", Agent.isOnNavMesh ? Agent.velocity.magnitude : 0, .12f, Time.deltaTime);
            if (!Agent.isOnNavMesh) return;
            if (Waiting) { if (Time.time >= waitUntil) PickDestination(); return; }
            if (Agent.pathPending) return;
            if (Agent.pathStatus == NavMeshPathStatus.PathComplete && Agent.remainingDistance <= Agent.stoppingDistance + .15f)
            {
                Arrivals++; Waiting = true; Agent.ResetPath(); waitUntil = Time.time + Random.Range(idleSeconds.x, idleSeconds.y); return;
            }
            if (Agent.pathStatus != NavMeshPathStatus.PathComplete || !Agent.hasPath) { PickDestination(); return; }
            if (Time.time >= progressTime)
            {
                if (Vector3.Distance(progressPosition, transform.position) < .12f) { StuckRecoveries++; PickDestination(); }
                else { progressPosition = transform.position; progressTime = Time.time + 2; }
            }
        }
        public void PickDestination()
        {
            for (int i = 0; i < 48; i++)
            {
                var p = new Vector3(Random.Range(roamingBounds.min.x, roamingBounds.max.x), transform.position.y,
                    Random.Range(roamingBounds.min.z, roamingBounds.max.z));
                if (!NavMesh.SamplePosition(p, out var hit, 2, Agent.areaMask) || Vector3.Distance(hit.position, transform.position) < 2) continue;
                if (!Agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                Agent.SetPath(path); Waiting = false; Destinations++; progressPosition = transform.position;
                progressTime = Time.time + 2; return;
            }
            Waiting = true; Agent.ResetPath(); waitUntil = Time.time + .5f;
        }
    }
}
