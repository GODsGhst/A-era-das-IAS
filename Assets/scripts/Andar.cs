using UnityEngine;
using UnityEngine.InputSystem;

public class Andar : MonoBehaviour
{
    [SerializeField] private float velocidade = 2f;
    [SerializeField] private float velocidadeCorrida = 3.8f; // Sprint (Shift): mais rápido que o Chase, mas faz barulho
    [SerializeField] private float gravidade = -9.81f;
    [SerializeField] private Transform modelo;  // malha do Mixamo, gira para a direção do movimento
    [SerializeField] private Animator animador; // parâmetro Speed (Idle/Walk)

    private CharacterController controlador;
    private PlayerNoise ruido;
    private InputAction mover;
    private InputAction correr;
    private float velocidadeVertical;

    private void Awake()
    {
        controlador = GetComponent<CharacterController>();
        ruido = GetComponent<PlayerNoise>();
        mover = InputSystem.actions.FindAction("Move", throwIfNotFound: false);
        correr = InputSystem.actions.FindAction("Sprint", throwIfNotFound: false);
    }

    private void OnEnable()
    {
        mover?.Enable();
        correr?.Enable();
    }

    private void OnDisable()
    {
        mover?.Disable();
        correr?.Disable();
    }

    private void Update()
    {
        if (mover == null || controlador == null)
            return;

        Vector2 entrada = mover.ReadValue<Vector2>();
        Vector3 direcao = new Vector3(entrada.x, 0f, entrada.y);

        bool correndo = correr != null && correr.IsPressed() && direcao != Vector3.zero;
        float velocidadeAtual = correndo ? velocidadeCorrida : velocidade;

        controlador.Move(direcao * velocidadeAtual * Time.deltaTime);

        if (ruido != null)
            ruido.isRunning = correndo;

        if (modelo != null && direcao.sqrMagnitude > 0.01f)
            modelo.rotation = Quaternion.Slerp(modelo.rotation, Quaternion.LookRotation(direcao), 10f * Time.deltaTime);

        if (animador != null)
            animador.SetFloat("Speed", direcao.magnitude * velocidadeAtual);

        if (controlador.isGrounded && velocidadeVertical < 0f)
            velocidadeVertical = -2f;

        velocidadeVertical += gravidade * Time.deltaTime;
        controlador.Move(Vector3.up * velocidadeVertical * Time.deltaTime);
    }
}
