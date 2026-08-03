# Code First

Uma máquina com um superstate e dois filhos. `Grounded` é o State de topo; dentro dele, `Idle` passa
para `Walk` quando existe input de movimento e `Walk` volta para `Idle` quando o input para. Quando o
personagem deixa de estar no chão, a ramificação inteira sai e `Airborne` assume. Nada é configurado
no Inspector e nada chama `Update` na mão.

```text
GroundedState
├── IdleState   (filho inicial)
└── WalkState

AirborneState
```

## Montando a cena

1. Adicione `ExampleInput` e `ExampleController` ao mesmo GameObject.
2. Atribua a referência de `ExampleInput` no controller.
3. Entre em Play Mode e mexa em `ExampleInput.Move` e `ExampleInput.IsGrounded`, pelo Inspector ou
   pelo seu próprio código de input.

## Como o exemplo é montado

### 1. Context

`ExampleContext` é uma classe comum com tudo o que os States precisam. Ele é criado pelo controller
e pertence àquela máquina.

### 2. States

`GroundedState`, `IdleState`, `WalkState` e `AirborneState` derivam de `FyniteState<ExampleContext>`.
Cada um sobrescreve só os callbacks que usa e lê `Context`, `DeltaTime` e `FixedDeltaTime` como
propriedades. Um State não sabe se é pai, filho ou plano.

### 3. Predicados

`HasMovement`, `HasNoMovement`, `IsGrounded` e `IsAirborne` implementam `IPredicate<ExampleContext>`
e respondem uma pergunta cada. Um predicado não muda nada.

### 4. Módulos de transição

`LocomotionTransitions` cuida de `Idle ↔ Walk` e `AirTransitions` cuida de `Grounded ↔ Airborne`. As
transições ficam aqui, nunca dentro dos States.

Como `Grounded` é o pai de `Idle` e `Walk`, a regra `Grounded → Airborne` vale para os dois filhos
sem precisar ser repetida: as transições do filho ativo são avaliadas primeiro e, quando nenhuma
vence, as do pai entram.

### 5. Attach

```csharp
using Fynite;

machine = Machine
    .Attach(this, context)
    .Start<GroundedState>()
    .Child<GroundedState, IdleState>()
    .Child<GroundedState, WalkState>()
    .Use<LocomotionTransitions>()
    .Use<AirTransitions>()
    .Build();
```

`Build()` entra em `GroundedState` e, em seguida, no seu filho inicial `IdleState` — o primeiro filho
declarado. Depois registra a máquina no Unity PlayerLoop.

Uma máquina sem nenhum `Child` continua sendo uma FSM plana; a hierarquia é opcional.

### 6. A máquina roda sozinha

`Update` e `FixedUpdate` são dirigidos pelo PlayerLoop, então o controller não tem nenhum dos dois.
Em cada frame, `Grounded` roda antes do filho ativo.

A máquina também é descartada automaticamente quando o owner é destruído: o `Exit` roda do filho para
o pai e o registro sai do loop sem que o controller precise de `OnDestroy`. `Dispose()` explícito só
é necessário quando a máquina deve ser encerrada antes da destruição do owner.
