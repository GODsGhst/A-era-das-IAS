using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    public Animator animator;
    public string speedParameter = "Speed";

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void SetSpeed(float speed)
    {
        if (animator != null)
            animator.SetFloat(speedParameter, speed);
    }

    public void SetAlert(bool alert)
    {
        if (animator != null)
            animator.SetBool("Alert", alert);
    }
}
