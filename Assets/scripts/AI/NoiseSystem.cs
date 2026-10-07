using UnityEngine;

// Aparece na estrutura de arquivos do Figma, mas sem código:
// avisa os inimigos dentro do raio que houve um barulho nesta posição.
public static class NoiseSystem
{
    public static void EmitNoise(Vector3 position, float radius, LayerMask enemyLayer)
    {
        Collider[] enemies = Physics.OverlapSphere(position, radius, enemyLayer);

        foreach (Collider col in enemies)
        {
            EnemyBrain brain = col.GetComponent<EnemyBrain>();

            if (brain != null)
                brain.ReceiveNoise(position);
        }
    }
}
