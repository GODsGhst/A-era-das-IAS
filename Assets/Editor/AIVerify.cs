using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Autoteste em Play Mode: Patrol → (jogador na frente) Chase → Attack com dano → (jogador some) Search → Patrol.
// Menu: Tools > IA > Testar IA (resultado no Console, linhas [Verify]).
// Batchmode: Unity -batchmode -projectPath . -executeMethod AIVerify.Run -logFile verify.log
[InitializeOnLoad]
public static class AIVerify
{
    const string Key = "AIVerify.Running";
    static int step;
    static float stepTime;
    static int failures;
    static bool sawAttackState;
    static Vector3 startPos;

    static AIVerify()
    {
        if (!SessionState.GetBool(Key, false))
            return;

        EditorApplication.update += Tick;
        Application.logMessageReceived += (msg, stack, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception))
                Check(false, "erro no console: " + msg);
        };
    }

    [MenuItem("Tools/IA/Testar IA (Play Mode)")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene("Assets/OutdoorsScene.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static void Check(bool ok, string what)
    {
        Debug.Log($"[Verify] {(ok ? "OK  " : "FAIL")} {what}");
        if (!ok) failures++;
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        Debug.Log($"[Verify] FIM: {failures} falha(s)");

        if (Application.isBatchMode)
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        else
            EditorApplication.ExitPlaymode();
    }

    static void Next()
    {
        step++;
        stepTime = Time.time;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying)
            return;

        var brain = Object.FindFirstObjectByType<EnemyBrain>();
        var player = GameObject.FindGameObjectWithTag("Player");
        if (brain == null || player == null)
            return;

        var animator = brain.GetComponent<Animator>();
        var agent = brain.GetComponent<NavMeshAgent>();
        var health = player.GetComponent<PlayerHealth>();
        string state = new SerializedObject(brain).FindProperty("currentStateName").stringValue;
        var animState = animator.GetCurrentAnimatorStateInfo(0);
        float elapsed = Time.time - stepTime;

        if (animState.IsName("Attack"))
            sawAttackState = true;

        if (Time.time > 90f)
        {
            Check(false, $"tempo esgotado no passo {step} (estado {state})");
            Finish();
            return;
        }

        switch (step)
        {
            case 0:
                if (Time.time < 0.5f) return;
                startPos = brain.transform.position;
                Next();
                break;

            case 1: // patrulhando
                if (elapsed < 6f) return;
                Check(state == "PatrolState", "estado inicial é PatrolState: " + state);
                Check(Vector3.Distance(startPos, brain.transform.position) > 3f, $"inimigo andou {Vector3.Distance(startPos, brain.transform.position):0.0} m na patrulha");
                Check(animState.IsName("Patrol"), "Animator no estado Patrol");
                Check(animator.GetFloat("Speed") > 0.8f, $"Speed = {animator.GetFloat("Speed"):0.00} (anda)");
                Check(!animator.GetBool("Alert"), "Alert = false na patrulha");
                Check(Mathf.Approximately(agent.speed, brain.movement.patrolSpeed), $"agent.speed = {agent.speed} (patrulha)");

                Teleport(player, brain.transform.position + brain.transform.forward * 6f);
                Next();
                break;

            case 2: // viu o jogador
                if (elapsed < 1f) return;
                Check(state == "ChaseState", "jogador na frente → ChaseState: " + state);
                Check(animator.GetBool("Alert"), "Alert = true no Chase");
                Check(animState.IsName("Chase") || animState.IsName("Attack"), "Animator no estado Chase");
                Check(Mathf.Approximately(agent.speed, brain.movement.chaseSpeed), $"agent.speed = {agent.speed} (perseguição)");
                Next();
                break;

            case 3: // alcança e ataca
                if (health.vida < 100f && !brain.attack.IsAttacking)
                {
                    Check(sawAttackState, "Animator passou pelo estado Attack");
                    Check(true, $"Animation Events AttackStart/AttackEnd: dano aplicado, vida = {health.vida}");
                    Check(!agent.isStopped, "NavMeshAgent liberado depois do AttackEnd");

                    // some da vista: vai para o ponto de patrulha mais longe
                    Transform far = brain.movement.patrolPoints[0];
                    foreach (var p in brain.movement.patrolPoints)
                        if (Vector3.Distance(p.position, brain.transform.position) > Vector3.Distance(far.position, brain.transform.position))
                            far = p;
                    Teleport(player, far.position);
                    Next();
                }
                else if (elapsed > 15f)
                {
                    Check(false, $"não atacou em 15 s (estado {state}, vida {health.vida}, distância {Vector3.Distance(brain.transform.position, player.transform.position):0.0})");
                    Finish();
                }
                break;

            case 4: // perdeu o jogador
                if (elapsed < 1f) return;
                Check(state == "SearchState", "perdeu o jogador → SearchState: " + state);
                Check(!animator.GetBool("Alert"), "Alert volta a false");
                Next();
                break;

            case 5: // volta a patrulhar
                if (elapsed < brain.searchTime + 1f) return;
                Check(state == "PatrolState", "depois do Search volta para PatrolState: " + state);
                Check(animState.IsName("Patrol"), "Animator de volta ao Patrol");
                Finish();
                break;
        }
    }

    static void Teleport(GameObject player, Vector3 position)
    {
        NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas);
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = hit.position;
        controller.enabled = true;
        Physics.SyncTransforms();
    }
}
