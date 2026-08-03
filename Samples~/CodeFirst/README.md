# Code First

Uma máquina com dois States: `Idle` passa para `Walk` quando existe input de movimento, e `Walk`
volta para `Idle` quando o input para. Nada é configurado no Inspector e nada chama `Update` na mão.

## Montando a cena

1. Adicione `ExampleInput` e `ExampleController` ao mesmo GameObject.
2. Atribua a referência de `ExampleInput` no controller.
3. Entre em Play Mode e mexa em `ExampleInput.Move`, pelo Inspector ou pelo seu próprio código de
   input.

## Como o exemplo é montado

### 1. Context

`ExampleContext` é uma classe comum com tudo o que os States precisam. Ele é criado pelo controller
e pertence àquela máquina.

### 2. States

`IdleState` e `WalkState` derivam de `FyniteState<ExampleContext>`. Cada um sobrescreve só os
callbacks que usa e lê `Context`, `DeltaTime` e `FixedDeltaTime` como propriedades.

### 3. Predicados

`HasMovement` e `HasNoMovement` implementam `IPredicate<ExampleContext>` e respondem uma pergunta
cada. Um predicado não muda nada.

### 4. Módulo de transições

`LocomotionTransitions` implementa `IFyniteTransitions<ExampleContext>` e declara qual State leva a
qual. As transições ficam aqui, nunca dentro dos States.

### 5. Attach

```csharp
using Fynite;

machine = Machine
    .Attach(this, context)
    .Start<IdleState>()
    .Use<LocomotionTransitions>()
    .Build();
```

`Build()` entra em `IdleState` e registra a máquina no Unity PlayerLoop.

### 6. A máquina roda sozinha

`Update` e `FixedUpdate` são dirigidos pelo PlayerLoop, então o controller não tem nenhum dos dois.

A máquina também é descartada automaticamente quando o owner é destruído: o `Exit` do State ativo
roda e o registro sai do loop sem que o controller precise de `OnDestroy`. `Dispose()` explícito só
é necessário quando a máquina deve ser encerrada antes da destruição do owner.
