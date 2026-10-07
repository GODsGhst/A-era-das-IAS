using UnityEngine;

public class SearchState : IEnemyState
{
    private EnemyBrain enemy;
    private float timer;

    public SearchState(EnemyBrain enemy)
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        timer = 0f;

        enemy.movement.MoveTo(
            enemy.GetLastKnownPosition());
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

        timer += Time.deltaTime;

        if (timer >= enemy.SearchDuration())
        {
            if (enemy.isPatroller)
                enemy.ChangeState(
                    new PatrolState(enemy));
            else
                enemy.ChangeState(
                    new WanderState(enemy));
        }
    }

    public void Exit()
    {
    }
}
