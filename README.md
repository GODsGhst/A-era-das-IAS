# A era das IAs — Jogo com IA e Animator

**Entrega – Jogo com IA e Animator (5,0 pontos)**

Jogo em Unity 6000.3.8f1 com um inimigo controlado por máquina de estados, NavMesh e Animator.

## Projeto

- Unity 6000.3.8f1, pipeline HDRP 17.3.0
- Pacotes: Input System 1.18.0 e AI Navigation 2.0.10 (`NavMeshSurface`)
- Cena principal: `Assets/OutdoorsScene.unity`

## Checklist da entrega

| Requisito | Onde está |
|---|---|
| 1. Animator configurado | `Assets/animations/EnemyController.controller` (inimigo) e `Assets/animations/PlayerController.controller` (jogador) |
| 2. Patrulha entre pontos | `PatrolState` e `EnemyMovement.GoToNextPatrolPoint()` com `NavMeshAgent`; pontos `PatrolPoint_A` a `PatrolPoint_D`, no objeto `PatrolPoints` |
| 3. Detecta o jogador e persegue | `EnemySensor.CanSeePlayer()` leva ao `ChaseState`; ao perder o jogador de vista, `SearchState` e depois `PatrolState` |
| 4. Animações do Mixamo por estado | Clipes `walk`, `Idle` e `attack`; `EnemyBrain.ChangeState` sincroniza `Alert` e velocidade; `EnemyBrain.Update` envia a velocidade do `NavMeshAgent` para `Speed` |

### Animator do inimigo

- Parâmetros: `Speed` (float), `Alert` (bool) e `Attack` (trigger).
- **Patrol**: Blend Tree 1D Idle ↔ Walk, andando a 1,3 m/s.
- **Chase**: Blend Tree 1D Idle ↔ Walk acelerado, a 3 m/s. Entra quando `Alert` é verdadeiro.
- **Attack**: Any State com a trigger `Attack`. Volta por Exit Time para Chase ou Patrol, conforme `Alert`.
- **Jogador** (`PlayerController.controller`): Blend Tree 1D com Idle, Walk (2 m/s) e Walk acelerado para a corrida (3,8 m/s).

## Arquitetura

Baseada na página 6 do Figma da disciplina.

| Componente | Responsabilidade |
|---|---|
| `EnemyBrain` | Máquina de estados. Guarda o estado atual (`IEnemyState`) e sincroniza `Alert` e velocidade com ele |
| `EnemySensor` | Visão: distância de 12 m, cone de 90° e `Physics.Linecast` contra a layer `Obstacle` |
| `EnemyMovement` | Movimento com `NavMeshAgent`: patrulha, destino aleatório e velocidades |
| `EnemyAnimator` | Envia `Speed` e `Alert` ao Animator |
| `EnemyCommunication` | Ao ver o jogador, avisa inimigos a até 15 m (`OverlapSphere`), que vão investigar |
| `NoiseSystem` e `PlayerNoise` | Correr (Shift) faz barulho num raio de 8 m; inimigos em patrulha vão investigar o ponto |
| `Attack` | Golpe a até 2 m, baseado na apostila de ataque (página 7) |

Estados (`IEnemyState`): `PatrolState`, `WanderState` (usado quando `isPatroller` é falso; vagueia por pontos aleatórios de 10 m), `InvestigateState`, `ChaseState` e `SearchState` (5 s na última posição vista).

Estrutura de `Assets/scripts`:

```
Assets/scripts/
├── Andar.cs                  Movimento do jogador (CharacterController + Input System)
├── AI/
│   ├── Attack.cs
│   ├── ChaseState.cs
│   ├── EnemyAnimator.cs
│   ├── EnemyBrain.cs
│   ├── EnemyCommunication.cs
│   ├── EnemyMovement.cs
│   ├── EnemySensor.cs
│   ├── IEnemyState.cs
│   ├── InvestigateState.cs
│   ├── NoiseSystem.cs
│   ├── PatrolState.cs
│   ├── SearchState.cs
│   └── WanderState.cs
└── Player/
    ├── PlayerHealth.cs       Vida do jogador; reinicia a cena ao zerar
    └── PlayerNoise.cs
```

## Ajustes sobre o código de referência

- **`AttackStart` e `AttackEnd`** no lugar de `Start` e `End` da apostila: `Start()` já é a mensagem da Unity usada na mesma classe, e duas `Start()` não compilam (erro CS0111).
- **`Alert` volta a `false` fora do Chase**: `EnemyBrain.ChangeState` define `Alert` conforme o estado. Sem isso, o inimigo continuaria em perseguição depois de perder o jogador.
- **Última posição vista atualizada durante o Chase**: a posição é atualizada enquanto o jogador está visível, então o Search vai até onde ele sumiu, e não até onde foi visto pela primeira vez.
- **Ângulo no plano horizontal e Linecast no peito**: de perto, o jogador fica bem abaixo do olho do inimigo; com ângulo 3D, ele sairia do cone justamente no ataque. O Linecast mira no peito porque o pivô do jogador fica nos pés.
- **Máquina de estados pausa durante o golpe**: `EnemyBrain.Update` não atualiza o estado enquanto `Attack.IsAttacking`. Sem isso, o Chase chamaria `MoveTo` e o inimigo andaria durante a animação.
- **Só ataca quem está vendo**: o `Attack` só conta os 0,5 s no alcance se `EnemySensor.CanSeePlayer()` for verdadeiro. Sem isso, durante a patrulha ele golpearia o jogador através da parede (a parede tem 1 m e o alcance do ataque é 2 m).

## Como testar

1. Abra o projeto no Unity 6000.3.8f1.
2. Abra `Assets/OutdoorsScene.unity` e aperte Play.

Controles: WASD, setas ou analógico para andar; Shift para correr (faz barulho).

- O inimigo patrulha. Entre no campo de visão dele para ser perseguido.
- Fique a menos de 2 m por 0,5 s e ele ataca. Cada golpe tira 10 de vida, de um total de 100. Ao zerar, a cena reinicia.
- Com Gizmos ligados, selecione o `Enemy`: a esfera amarela e as bordas do cone mostram o alcance de visão; a esfera vermelha mostra a área do golpe.
- No Inspector do `EnemyBrain`, o campo **Current State Name** mostra o estado atual durante o Play. O Console registra cada troca de estado.

## Ferramentas de editor

- **Tools > IA > Configurar cena** (`Assets/Editor/EnemyAISetup.cs`): recria os Animator Controllers, o prefab `Assets/Prefabs/Enemy.prefab`, o NavMesh, os pontos de patrulha, o inimigo e o jogador na cena. Pode ser executado de novo.
- **Tools > IA > Testar IA (Play Mode)** (`Assets/Editor/AIVerify.cs`): abre a cena, entra em Play e roda um autoteste que confere Patrol → Chase → Attack → Search → Patrol. O resultado sai no Console, nas linhas `[Verify]`.

## Uso de IA

Eu criei o labirinto (chão e paredes), importei os personagens e as animações do Mixamo (Idle, walk e attack) e fiz o script base de movimento do jogador (`Andar.cs`). Usei o Claude (IA da Anthropic) para me ajudar a montar o inimigo, o NavMesh e o Animator, implementar os scripts da IA a partir do material do Figma da disciplina e corrigir bugs.

## Observação sobre o download

Os modelos `.fbx` estão no Git LFS. Para baixar o projeto, use `git clone` com o Git LFS instalado. O "Download ZIP" do GitHub só inclui esses arquivos se a opção **Include Git LFS objects in archives** estiver ativada nas configurações do repositório.
