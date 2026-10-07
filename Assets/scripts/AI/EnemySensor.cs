using UnityEngine;

public class EnemySensor : MonoBehaviour
{
    [Header("Player")]
    public Transform player;

    [Header("Vision")]
    public Transform eyePoint;
    public float viewDistance = 12f;
    [Range(0, 360)]
    public float viewAngle = 90f;

    [Header("Optional SphereCast")]
    public float sphereRadius = 0.35f;

    [Header("Layers")]
    public LayerMask obstacleMask;

    public Vector3 LastKnownPosition { get; private set; }

    public bool CanSeePlayer()
    {
        if (player == null)
            return false;

        Vector3 origin = eyePoint != null
            ? eyePoint.position
            : transform.position + Vector3.up;

        // Mira no peito do jogador (o pivô dele fica nos pés, no chão).
        Vector3 target = player.position + Vector3.up;

        Vector3 direction = target - origin;

        float distance = direction.magnitude;

        if (distance > viewDistance)
            return false;

        // Ângulo medido só no plano horizontal: de perto o jogador fica bem abaixo
        // do olho e, com o ângulo 3D, sairia do cone de visão justamente no ataque.
        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);

        float angle = Vector3.Angle(
            transform.forward,
            flatDirection);

        if (angle > viewAngle / 2f)
            return false;

        // Confirma se existe obstáculo entre o inimigo e o jogador.
        if (Physics.Linecast(
            origin,
            target,
            out RaycastHit hit,
            obstacleMask))
        {
            return false;
        }

        LastKnownPosition = player.position;
        return true;
    }

    public bool SphereDetectPlayer()
    {
        if (player == null)
            return false;

        Vector3 origin = eyePoint != null
            ? eyePoint.position
            : transform.position + Vector3.up;

        Vector3 direction =
            transform.forward;

        if (Physics.SphereCast(
            origin,
            sphereRadius,
            direction,
            out RaycastHit hit,
            viewDistance))
        {
            if (hit.transform.CompareTag("Player"))
            {
                LastKnownPosition = player.position;
                return true;
            }
        }

        return false;
    }

    public bool RaycastDetectPlayer()
    {
        if (player == null)
            return false;

        Vector3 origin = eyePoint != null
            ? eyePoint.position
            : transform.position + Vector3.up;

        if (Physics.Raycast(
            origin,
            transform.forward,
            out RaycastHit hit,
            viewDistance))
        {
            if (hit.transform.CompareTag("Player"))
            {
                LastKnownPosition = player.position;
                return true;
            }
        }

        return false;
    }

    // Selecione o inimigo com Gizmos ligado para ver o alcance e o cone de visão.
    void OnDrawGizmosSelected()
    {
        Vector3 origin = eyePoint != null
            ? eyePoint.position
            : transform.position + Vector3.up;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, viewDistance);

        Vector3 left = Quaternion.Euler(0f, -viewAngle / 2f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle / 2f, 0f) * transform.forward;
        Gizmos.DrawLine(origin, origin + left * viewDistance);
        Gizmos.DrawLine(origin, origin + right * viewDistance);
    }
}
