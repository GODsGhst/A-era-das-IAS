using UnityEngine;

public class PatrolState : IEnemyState
{
    private EnemyBrain enemy;

    public PatrolState(EnemyBrain enemy)
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        enemy.movement.GoToNextPatrolPoint();
    }

    public void Update()
    {
        if (enemy.CanSeePlayer())
        {
            enemy.PlayerSeen();
            enemy.ChangeState(
                new ChaseState(enemy));
            return;
        }

        if (enemy.ConsumeAlert(out Vector3 alertPosition))
        {
            enemy.ChangeState(
                new InvestigateState(
                    enemy,
                    alertPosition));
            return;
        }

        if (enemy.ConsumeNoise(out Vector3 noisePosition))
        {
            enemy.ChangeState(
                new InvestigateState(
                    enemy,
                    noisePosition));
            return;
        }

        if (enemy.movement.ReachedDestination())
        {
            enemy.movement.GoToNextPatrolPoint();
        }
    }

    public void Exit()
    {
    }
}
