using UnityEngine;

public class ChaseState : IEnemyState
{
    private EnemyBrain enemy;

    public ChaseState(EnemyBrain enemy)
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        enemy.movement.agent.isStopped = false;
    }

    public void Update()
    {
        if (enemy.sensor == null ||
            enemy.sensor.player == null)
            return;

        if (enemy.CanSeePlayer())
        {
            enemy.movement.MoveTo(
                enemy.sensor.player.position);
        }
        else
        {
            enemy.ChangeState(
                new SearchState(enemy));
        }
    }

    public void Exit()
    {
    }
}
