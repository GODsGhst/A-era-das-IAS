using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    public float vida = 100f;

    // Chamado pelo Attack.cs do inimigo via SendMessage("TakeDamage", dano).
    public void TakeDamage(float dano)
    {
        vida -= dano;
        Debug.Log($"Player levou {dano} de dano. Vida: {vida}");

        if (vida <= 0f)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
