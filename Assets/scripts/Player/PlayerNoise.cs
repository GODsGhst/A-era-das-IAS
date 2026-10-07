using UnityEngine;

public class PlayerNoise : MonoBehaviour
{
    public LayerMask enemyLayer;

    public float walkNoiseRadius = 0f;
    public float runNoiseRadius = 8f;

    public bool isRunning;

    void Update()
    {
        if (isRunning)
        {
            NoiseSystem.EmitNoise(
                transform.position,
                runNoiseRadius,
                enemyLayer);
        }
    }

    public void MakeNoise(float radius)
    {
        NoiseSystem.EmitNoise(
            transform.position,
            radius,
            enemyLayer);
    }
}
