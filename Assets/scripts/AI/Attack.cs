using UnityEngine;
using UnityEngine.AI;

// Baseado na apostila "Ataque de Inimigo" (Animator + Animation Events).
// Ajuste: na apostila os eventos se chamam Start() e End(), mas Start() já é a mensagem
// da Unity usada logo abaixo (duas Start() na mesma classe não compilam: erro CS0111).
// Por isso os Animation Events do clipe attack chamam AttackStart() e AttackEnd().
public class Attack : MonoBehaviour
{
    [Header("Ataque")]
    public float attackRange = 2f;
    public float timeToAttack = 0.5f;
    public float damage = 10f;

    [Header("Colisão do ataque")]
    public Transform attackPoint;
    public float attackRadius = 1f;

    private Transform player;
    private NavMeshAgent agent;
    private Animator animator;
    private EnemySensor sensor;

    private float timeInRange = 0f;
    private bool attacking = false;
    private bool checkingHit = false;
    private bool alreadyHit = false;

    public bool IsAttacking => attacking;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        sensor = GetComponent<EnemySensor>();

        GameObject obj = GameObject.FindGameObjectWithTag("Player");

        if (obj != null)
            player = obj.transform;
    }

    void Update()
    {
        if (player == null)
            return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (attacking)
        {
            if (checkingHit)
                CheckHit();

            return;
        }

        // Ajuste: só conta o tempo no alcance se o inimigo estiver vendo o jogador
        // (sem isso, na patrulha ele golpeia através da parede, que tem 1 m e o alcance é 2 m).
        if (distance <= attackRange && (sensor == null || sensor.CanSeePlayer()))
        {
            timeInRange += Time.deltaTime;

            if (timeInRange >= timeToAttack)
            {
                StartAttack();
            }
        }
        else
        {
            timeInRange = 0f;
        }
    }

    void StartAttack()
    {
        attacking = true;
        timeInRange = 0f;

        agent.isStopped = true;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);

        animator.SetTrigger("Attack");
    }

    // Animation Event:
    // Colocado no frame em que o golpe deve causar dano.
    public void AttackStart()
    {
        checkingHit = true;
        alreadyHit = false;
    }

    // Animation Event:
    // Colocado no final da janela de dano.
    public void AttackEnd()
    {
        checkingHit = false;
        attacking = false;

        agent.isStopped = false;
    }

    void CheckHit()
    {
        if (alreadyHit || attackPoint == null)
            return;

        Collider[] hits = Physics.OverlapSphere(
            attackPoint.position,
            attackRadius
        );

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                alreadyHit = true;

                hit.SendMessage(
                    "TakeDamage",
                    damage,
                    SendMessageOptions.DontRequireReceiver
                );

                Debug.Log("Player atingido!");
                break;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            attackPoint.position,
            attackRadius
        );
    }
}
