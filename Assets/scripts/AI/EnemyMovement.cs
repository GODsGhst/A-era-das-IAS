using UnityEngine;
using UnityEngine.AI;

public class EnemyMovement : MonoBehaviour
{
    public NavMeshAgent agent;

    public Transform[] patrolPoints;

    [Header("Velocidades")]
    public float patrolSpeed = 1.3f; // casa com o passo da animação walk do Mixamo
    public float chaseSpeed = 3f;

    private int currentPoint = 0;

    void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    public void SetChasing(bool chasing)
    {
        agent.speed = chasing ? chaseSpeed : patrolSpeed;
    }

    public void Stop()
    {
        agent.isStopped = true;
    }

    public void MoveTo(Vector3 position)
    {
        agent.isStopped = false;
        agent.SetDestination(position);
    }

    public bool ReachedDestination()
    {
        if (agent.pathPending)
            return false;

        return agent.remainingDistance <= agent.stoppingDistance;
    }

    public void GoToNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        MoveTo(patrolPoints[currentPoint].position);

        currentPoint++;

        if (currentPoint >= patrolPoints.Length)
            currentPoint = 0;
    }

    public bool GoToRandomPoint(float radius)
    {
        Vector3 randomDirection =
            Random.insideUnitSphere * radius;

        randomDirection += transform.position;

        if (NavMesh.SamplePosition(
            randomDirection,
            out NavMeshHit hit,
            radius,
            NavMesh.AllAreas))
        {
            MoveTo(hit.position);
            return true;
        }

        return false;
    }
}
