using UnityEngine;

public class EnemyCommunication : MonoBehaviour
{
    public float alertRadius = 15f;
    public LayerMask enemyLayer;

    public void AlertNearbyEnemies(Vector3 playerPosition)
    {
        Collider[] enemies =
            Physics.OverlapSphere(
                transform.position,
                alertRadius,
                enemyLayer);

        foreach (Collider col in enemies)
        {
            EnemyBrain brain =
                col.GetComponent<EnemyBrain>();

            if (brain != null && brain.gameObject != gameObject)
            {
                brain.ReceiveAlert(playerPosition);
            }
        }
    }
}
