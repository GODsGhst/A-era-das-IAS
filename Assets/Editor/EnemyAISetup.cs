using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Monta a cena da entrega: Animator do inimigo (Patrol / Chase / Attack), prefab do inimigo com a IA da
// página 6 do Figma, NavMesh, PatrolPoint_A..D e o Player. Menu: Tools > IA > Configurar cena.
// Pode rodar de novo: recria controllers, prefab, NavMesh e o inimigo da cena.
public static class EnemyAISetup
{
    const string ScenePath = "Assets/OutdoorsScene.unity";
    const string AnimDir = "Assets/animations/";
    const string EnemyFbxPath = "Assets/3D/enemy.fbx";
    const string PlayerFbxPath = "Assets/3D/protagonist.fbx";
    const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
    const string NavMeshPath = "Assets/OutdoorsScene/NavMesh.asset";

    const float WalkClipSpeed = 1.28f; // o walk.anim anda 1,32 m em 1,03 s
    const float PatrolSpeed = 1.3f;
    const float ChaseSpeed = 3f;
    const float PlayerWalkSpeed = 2f;
    const float PlayerRunSpeed = 3.8f;

    static readonly Vector3[] PatrolPositions =
    {
        new Vector3(-14.5f, 0f, 20f), new Vector3(13f, 0f, 20f), new Vector3(13f, 0f, -21f), new Vector3(-14.5f, 0f, -21f)
    };
    static readonly string[] PatrolNames = { "PatrolPoint_A", "PatrolPoint_B", "PatrolPoint_C", "PatrolPoint_D" };
    static readonly Vector3 PlayerStart = new Vector3(-2f, 0f, -7.3f);

    [MenuItem("Tools/IA/Configurar cena")]
    public static void Run()
    {
        int obstacleLayer = EnsureLayer("Obstacle");
        int enemyLayer = EnsureLayer("Enemy");
        int playerLayer = EnsureLayer("Player");

        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + "Idle.anim");
        var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + "walk.anim");
        var attack = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + "attack.anim");
        var enemyFbx = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFbxPath);
        var playerFbx = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFbxPath);

        RemoveForwardDrift(walk);
        SetAttackEvents(attack, enemyFbx);

        var enemyController = BuildEnemyController(idle, walk, attack);
        var enemyPrefab = BuildEnemyPrefab(enemyFbx, enemyController, enemyLayer, obstacleLayer);

        // O protagonista usa ossos "mixamorig1:", então as animações são copiadas com o prefixo dele.
        float hipsScale = playerFbx.transform.Find("mixamorig1:Hips").localPosition.y
                        / enemyFbx.transform.Find("mixamorig:Hips").localPosition.y;
        var playerIdle = RetargetToProtagonist(idle, AnimDir + "Player_Idle.anim", hipsScale);
        var playerWalk = RetargetToProtagonist(walk, AnimDir + "Player_Walk.anim", hipsScale);
        var playerController = BuildPlayerController(playerIdle, playerWalk, WalkClipSpeed * hipsScale);

        var scene = EditorSceneManager.OpenScene(ScenePath);

        FixFloorCollider();

        foreach (var root in scene.GetRootGameObjects().Where(g => g.name.StartsWith("Cube")))
            root.layer = obstacleLayer; // paredes: bloqueiam o Linecast da visão

        var player = SetupPlayer(playerController, playerLayer, enemyLayer);

        BakeNavMesh(~((1 << enemyLayer) | (1 << playerLayer)));

        var points = SetupPatrolPoints();
        SetupEnemy(enemyPrefab, points, player.transform, scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Setup] Cena configurada.");
    }

    static int EnsureLayer(string name)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");

        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == name)
                return i;

        for (int i = 8; i < layers.arraySize; i++)
        {
            var layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue))
                continue;

            layer.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return i;
        }

        throw new System.Exception("Não há layer livre para " + name);
    }

    // O walk do Mixamo não é "in place": o Hips anda para frente e volta ao início a cada ciclo.
    // Tira a tendência linear do eixo Z para quem move o inimigo ser só o NavMeshAgent.
    static void RemoveForwardDrift(AnimationClip clip)
    {
        var binding = EditorCurveBinding.FloatCurve("mixamorig:Hips", typeof(Transform), "m_LocalPosition.z");
        var curve = AnimationUtility.GetEditorCurve(clip, binding);
        var keys = curve.keys;
        float drift = keys[^1].value - keys[0].value;

        if (Mathf.Abs(drift) < 0.01f)
            return;

        float slope = drift / keys[^1].time;
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i].value -= slope * keys[i].time;
            keys[i].inTangent -= slope;
            keys[i].outTangent -= slope;
        }

        curve.keys = keys;
        AnimationUtility.SetEditorCurve(clip, binding, curve);
        Debug.Log($"[Setup] {clip.name}: removido avanço de {drift:0.00} m por ciclo");
    }

    // Animation Events da apostila: AttackStart no impacto, AttackEnd no fim da janela de dano.
    // O impacto é o frame em que mão/pé chega mais à frente do corpo.
    static void SetAttackEvents(AnimationClip clip, GameObject model)
    {
        var go = Object.Instantiate(model);
        var bones = new[] { "mixamorig:RightHand", "mixamorig:LeftHand", "mixamorig:RightFoot", "mixamorig:LeftFoot" }
            .Select(n => go.GetComponentsInChildren<Transform>().First(t => t.name == n)).ToArray();

        clip.SampleAnimation(go, 0f);
        float[] rest = bones.Select(b => go.transform.InverseTransformPoint(b.position).z).ToArray();

        float bestReach = float.MinValue;
        float impact = clip.length * 0.4f;
        for (float t = 0f; t <= clip.length; t += 1f / 60f)
        {
            clip.SampleAnimation(go, t);
            for (int i = 0; i < bones.Length; i++)
            {
                float reach = go.transform.InverseTransformPoint(bones[i].position).z - rest[i];
                if (reach > bestReach)
                {
                    bestReach = reach;
                    impact = t;
                }
            }
        }
        Object.DestroyImmediate(go);

        float start = Mathf.Clamp(impact - 0.1f, 0f, clip.length * 0.75f);
        float end = Mathf.Clamp(impact + 0.25f, start + 0.1f, clip.length * 0.85f);
        AnimationUtility.SetAnimationEvents(clip, new[]
        {
            new AnimationEvent { time = start, functionName = "AttackStart" },
            new AnimationEvent { time = end, functionName = "AttackEnd" },
        });
        Debug.Log($"[Setup] attack: impacto em {impact:0.00}s → AttackStart {start:0.00}s, AttackEnd {end:0.00}s (clipe {clip.length:0.00}s)");
    }

    // Parâmetros: Speed (velocidade do NavMeshAgent), Alert (true no Chase), Attack (Trigger da apostila).
    // Patrol = Blend Tree Idle↔Walk; Chase = Blend Tree Idle↔Walk acelerado (corrida); Attack via Any State.
    static AnimatorController BuildEnemyController(AnimationClip idle, AnimationClip walk, AnimationClip attack)
    {
        string path = AnimDir + "EnemyController.controller";
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Alert", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var stateMachine = controller.layers[0].stateMachine;
        var patrol = AddLocomotion(controller, "Patrol", idle, (walk, PatrolSpeed, PatrolSpeed / WalkClipSpeed));
        var chase = AddLocomotion(controller, "Chase", idle, (walk, ChaseSpeed, ChaseSpeed / WalkClipSpeed));
        var attackState = stateMachine.AddState("Attack");
        attackState.motion = attack;
        stateMachine.defaultState = patrol;

        AddTransition(patrol, chase, AnimatorConditionMode.If, "Alert");
        AddTransition(chase, patrol, AnimatorConditionMode.IfNot, "Alert");

        var toAttack = stateMachine.AddAnyStateTransition(attackState);
        toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        toAttack.hasExitTime = false;
        toAttack.duration = 0.1f;
        toAttack.canTransitionToSelf = false;

        AddExitTransition(attackState, chase, AnimatorConditionMode.If, "Alert");
        AddExitTransition(attackState, patrol, AnimatorConditionMode.IfNot, "Alert");
        return controller;
    }

    static AnimatorController BuildPlayerController(AnimationClip idle, AnimationClip walk, float walkClipSpeed)
    {
        string path = AnimDir + "PlayerController.controller";
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        AddLocomotion(controller, "Locomotion", idle,
            (walk, PlayerWalkSpeed, PlayerWalkSpeed / walkClipSpeed),
            (walk, PlayerRunSpeed, PlayerRunSpeed / walkClipSpeed));
        return controller;
    }

    // Blend Tree 1D no parâmetro Speed: Idle em 0 e o walk acelerado (timeScale) para o passo casar com a velocidade.
    static AnimatorState AddLocomotion(AnimatorController controller, string name, AnimationClip idle,
        params (AnimationClip clip, float speed, float timeScale)[] moves)
    {
        var state = controller.CreateBlendTreeInController(name, out BlendTree tree, 0);
        tree.name = name;
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        foreach (var move in moves)
            tree.AddChild(move.clip, move.speed);

        var children = tree.children;
        for (int i = 0; i < moves.Length; i++)
            children[i + 1].timeScale = moves[i].timeScale;
        tree.children = children;
        return state;
    }

    static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string parameter)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.2f;
        transition.AddCondition(mode, 0f, parameter);
    }

    static void AddExitTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string parameter)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 0.9f;
        transition.duration = 0.15f;
        transition.AddCondition(mode, 0f, parameter);
    }

    static AnimationClip RetargetToProtagonist(AnimationClip source, string path, float hipsScale)
    {
        var clip = new AnimationClip { frameRate = source.frameRate };

        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            if (binding.propertyName.StartsWith("m_LocalScale"))
                continue;

            var curve = AnimationUtility.GetEditorCurve(source, binding);
            if (binding.propertyName.StartsWith("m_LocalPosition"))
            {
                var keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i].value *= hipsScale;
                    keys[i].inTangent *= hipsScale;
                    keys[i].outTangent *= hipsScale;
                }
                curve.keys = keys;
            }

            var target = binding;
            target.path = binding.path.Replace("mixamorig:", "mixamorig1:");
            AnimationUtility.SetEditorCurve(clip, target, curve);
        }

        AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(source));
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static GameObject BuildEnemyPrefab(GameObject fbx, AnimatorController controller, int enemyLayer, int obstacleLayer)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Enemy";
        go.layer = enemyLayer;

        var animator = GetOrAdd<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var agent = GetOrAdd<NavMeshAgent>(go);
        agent.radius = 0.35f;
        agent.height = 1.9f;
        agent.speed = PatrolSpeed;
        agent.angularSpeed = 360f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 1f;

        var body = GetOrAdd<CapsuleCollider>(go);
        body.center = new Vector3(0f, 0.95f, 0f);
        body.radius = 0.35f;
        body.height = 1.9f;

        var eyePoint = AddChild(go, "EyePoint", new Vector3(0f, 1.65f, 0.15f));
        var attackPoint = AddChild(go, "AttackPoint", new Vector3(0f, 1f, 0.8f));

        var sensor = GetOrAdd<EnemySensor>(go);
        sensor.eyePoint = eyePoint;
        sensor.obstacleMask = 1 << obstacleLayer;

        var movement = GetOrAdd<EnemyMovement>(go);
        movement.agent = agent;
        movement.patrolSpeed = PatrolSpeed;
        movement.chaseSpeed = ChaseSpeed;

        var enemyAnimator = GetOrAdd<EnemyAnimator>(go);
        enemyAnimator.animator = animator;

        var communication = GetOrAdd<EnemyCommunication>(go);
        communication.enemyLayer = 1 << enemyLayer;

        var attack = GetOrAdd<Attack>(go);
        attack.attackPoint = attackPoint;

        var brain = GetOrAdd<EnemyBrain>(go);
        brain.sensor = sensor;
        brain.movement = movement;
        brain.enemyAnimator = enemyAnimator;
        brain.communication = communication;
        brain.attack = attack;

        Directory.CreateDirectory(Path.GetDirectoryName(EnemyPrefabPath));
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, EnemyPrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // O "Square" é um sprite com escala 90: o BoxCollider dele (espessura 0,2) ficava com 18 m,
    // com o topo em y = 9, acima das paredes. Deixa o colisor com 1 m logo abaixo do sprite (y de -1 a 0).
    static void FixFloorCollider()
    {
        var floor = GameObject.Find("Square");
        var collider = floor.GetComponent<BoxCollider>();
        float thickness = floor.transform.lossyScale.z;
        float down = Vector3.Dot(floor.transform.forward, Vector3.down) > 0f ? 1f : -1f;
        collider.size = new Vector3(1f, 1f, 1f / thickness);
        collider.center = new Vector3(0f, 0f, down * 0.5f / thickness);
        Physics.SyncTransforms();
        Debug.Log($"[Setup] Chão: colisor de y={collider.bounds.min.y:0.00} até y={collider.bounds.max.y:0.00}");
    }

    static GameObject SetupPlayer(AnimatorController controller, int playerLayer, int enemyLayer)
    {
        var player = GameObject.Find("Player");
        if (player == null)
            player = new GameObject("Player");

        player.tag = "Player";
        player.layer = playerLayer;
        player.transform.SetPositionAndRotation(PlayerStart, Quaternion.identity);

        var model = GameObject.Find("protagonist");
        model.transform.SetParent(player.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        // O jogador é movido pelo CharacterController (Andar), não por NavMesh.
        foreach (var agent in model.GetComponents<NavMeshAgent>())
            Object.DestroyImmediate(agent);
        foreach (var capsule in model.GetComponents<CapsuleCollider>())
            Object.DestroyImmediate(capsule);

        var animator = GetOrAdd<Animator>(model);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        var characterController = GetOrAdd<CharacterController>(player);
        characterController.height = 1.9f;
        characterController.radius = 0.35f;
        characterController.center = new Vector3(0f, 0.95f + characterController.skinWidth, 0f);

        var andar = new SerializedObject(GetOrAdd<Andar>(player));
        andar.FindProperty("velocidade").floatValue = PlayerWalkSpeed;
        andar.FindProperty("velocidadeCorrida").floatValue = PlayerRunSpeed;
        andar.FindProperty("modelo").objectReferenceValue = model.transform;
        andar.FindProperty("animador").objectReferenceValue = animator;
        andar.ApplyModifiedPropertiesWithoutUndo();

        GetOrAdd<PlayerNoise>(player).enemyLayer = 1 << enemyLayer;
        GetOrAdd<PlayerHealth>(player);

        // Câmera segue o jogador de cima (o root do Player não gira, só o modelo).
        var camera = GameObject.Find("Main Camera");
        camera.transform.SetParent(player.transform, false);
        camera.transform.localPosition = new Vector3(0f, 16f, -9f);
        camera.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);

        return player;
    }

    static void BakeNavMesh(int layerMask)
    {
        var go = GameObject.Find("NavMesh");
        if (go == null)
            go = new GameObject("NavMesh");

        var surface = GetOrAdd<NavMeshSurface>(go);
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = layerMask;
        surface.BuildNavMesh();

        AssetDatabase.DeleteAsset(NavMeshPath);
        Directory.CreateDirectory(Path.GetDirectoryName(NavMeshPath));
        AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        EditorUtility.SetDirty(surface);

        var triangulation = NavMesh.CalculateTriangulation();
        Debug.Log($"[Setup] NavMesh: {triangulation.indices.Length / 3} triângulos");
    }

    static Transform[] SetupPatrolPoints()
    {
        var root = GameObject.Find("PatrolPoints");
        if (root == null)
            root = new GameObject("PatrolPoints");

        var points = new Transform[PatrolNames.Length];
        for (int i = 0; i < points.Length; i++)
        {
            var point = root.transform.Find(PatrolNames[i]);
            if (point == null)
                point = AddChild(root, PatrolNames[i], Vector3.zero);

            if (!NavMesh.SamplePosition(PatrolPositions[i], out NavMeshHit hit, 3f, NavMesh.AllAreas))
                throw new System.Exception(PatrolNames[i] + " fora do NavMesh");

            point.position = hit.position;
            points[i] = point;
        }

        for (int i = 0; i < points.Length; i++)
        {
            var path = new NavMeshPath();
            var next = points[(i + 1) % points.Length];
            NavMesh.CalculatePath(points[i].position, next.position, NavMesh.AllAreas, path);
            float length = 0f;
            for (int c = 1; c < path.corners.Length; c++)
                length += Vector3.Distance(path.corners[c - 1], path.corners[c]);
            Debug.Log($"[Setup] {points[i].name} → {next.name}: {path.status}, {length:0.0} m");
            if (path.status != NavMeshPathStatus.PathComplete)
                throw new System.Exception("Caminho de patrulha incompleto: " + points[i].name);
        }
        return points;
    }

    static void SetupEnemy(GameObject prefab, Transform[] points, Transform player, UnityEngine.SceneManagement.Scene scene)
    {
        var old = GameObject.Find("Enemy");
        if (old != null)
            Object.DestroyImmediate(old);

        var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        enemy.transform.position = points[0].position;
        enemy.transform.rotation = Quaternion.LookRotation(points[1].position - points[0].position);

        var movement = enemy.GetComponent<EnemyMovement>();
        movement.patrolPoints = points;
        PrefabUtility.RecordPrefabInstancePropertyModifications(movement);

        var sensor = enemy.GetComponent<EnemySensor>();
        sensor.player = player;
        PrefabUtility.RecordPrefabInstancePropertyModifications(sensor);
    }

    static Transform AddChild(GameObject parent, string name, Vector3 localPosition)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent.transform, false);
        child.localPosition = localPosition;
        return child;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        return go.TryGetComponent(out T component) ? component : go.AddComponent<T>();
    }
}
