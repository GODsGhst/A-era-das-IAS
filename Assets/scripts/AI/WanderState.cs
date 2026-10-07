using UnityEngine;

public class WanderState : IEnemyState
{
    private EnemyBrain enemy;

    public WanderState(EnemyBrain enemy)
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        ChooseDestination();
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
            ChooseDestination();
        }
    }

    void ChooseDestination()
    {
        enemy.movement.GoToRandomPoint(10f);
    }

    public void Exit()
    {
    }
}
