using UnityEngine;

public class EnemyBrain : MonoBehaviour
{
    [Header("Systems")]
    public EnemySensor sensor;
    public EnemyMovement movement;
    public EnemyAnimator enemyAnimator;
    public EnemyCommunication communication;
    public Attack attack;

    [Header("Behavior")]
    public bool isPatroller = true;
    public float searchTime = 5f;

    [Header("Debug")]
    [SerializeField] private string currentStateName; // estado atual, visível no Inspector durante o Play

    private IEnemyState currentState;

    private Vector3 lastKnownPosition;
    private bool hasNoise;
    private bool hasAlert;

    void Start()
    {
        if (sensor != null && sensor.player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                sensor.player = playerObject.transform;
        }

        if (isPatroller)
            ChangeState(new PatrolState(this));
        else
            ChangeState(new WanderState(this));
    }

    void Update()
    {
        // Enquanto o golpe acontece (Trigger Attack → evento AttackEnd) a máquina de estados espera,
        // senão o Chase chamaria MoveTo e o inimigo andaria durante a animação de ataque.
        if (attack == null || !attack.IsAttacking)
            currentState?.Update();

        if (enemyAnimator != null && movement != null)
        {
            enemyAnimator.SetSpeed(
                movement.agent.velocity.magnitude);
        }
    }

    public void ChangeState(IEnemyState newState)
    {
        currentState?.Exit();

        currentState = newState;

        // Sincroniza IA → Animator: no Chase o inimigo corre (velocidade maior + Alert liga a
        // Blend Tree de perseguição); em qualquer outro estado volta para a velocidade/animação de patrulha.
        bool chasing = newState is ChaseState;

        if (movement != null)
            movement.SetChasing(chasing);

        if (enemyAnimator != null)
            enemyAnimator.SetAlert(chasing);

        currentStateName = newState?.GetType().Name;
        Debug.Log($"{name}: {currentStateName}");

        currentState?.Enter();
    }

    public bool CanSeePlayer()
    {
        if (sensor == null || !sensor.CanSeePlayer())
            return false;

        // Mantém a última posição vista atualizada durante a perseguição,
        // para o Search ir até onde o jogador sumiu (e não onde foi visto pela primeira vez).
        lastKnownPosition = sensor.LastKnownPosition;
        return true;
    }

    public Vector3 GetLastKnownPosition()
    {
        return lastKnownPosition;
    }

    public void ReceiveNoise(Vector3 position)
    {
        lastKnownPosition = position;
        hasNoise = true;
    }

    public void ReceiveAlert(Vector3 position)
    {
        lastKnownPosition = position;
        hasAlert = true;
    }

    public bool ConsumeNoise(out Vector3 position)
    {
        position = lastKnownPosition;

        if (!hasNoise)
            return false;

        hasNoise = false;
        return true;
    }

    public bool ConsumeAlert(out Vector3 position)
    {
        position = lastKnownPosition;

        if (!hasAlert)
            return false;

        hasAlert = false;
        return true;
    }

    public void PlayerSeen()
    {
        lastKnownPosition = sensor.player.position;

        if (communication != null)
        {
            communication.AlertNearbyEnemies(
                lastKnownPosition);
        }

        if (enemyAnimator != null)
            enemyAnimator.SetAlert(true);
    }

    public float SearchDuration()
    {
        return searchTime;
    }
}
