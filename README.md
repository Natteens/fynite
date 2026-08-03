# Fynite

State machine code-first, type-safe e dirigida automaticamente pelo Unity PlayerLoop.

```csharp
using Fynite;

machine = Machine
    .Attach(this, context)
    .Start<IdleState>()
    .Use<LocomotionTransitions>()
    .Build();
```

É isso. Não existe `machine.Update()`, `machine.Tick(deltaTime)` nem `machine.Start()` no seu
código: `Build()` entra no State inicial e registra a máquina no PlayerLoop.

## 📥 Instalação

Este pacote pode ser instalado através do Unity Package Manager usando a URL do Git.

**Sempre instale com uma tag.** Uma URL sem `#<tag>` resolve para o commit atual da branch
`main`, que é uma revisão de desenvolvimento: ela pode declarar o mesmo número de versão do
último release e ainda assim conter código diferente. Dois projetos instalados em dias
diferentes receberiam revisões distintas sob o mesmo `version`, e o Package Manager não avisa.

A última versão publicada é **`v0.7.0`**.

### Via Package Manager (Recomendado)

1. Abra o Package Manager (Window > Package Manager)
2. Clique no botão **+** no canto superior esquerdo
3. Selecione **"Add package from git URL..."**
4. Digite a URL: `https://github.com/Natteens/fynite.git#v0.7.0`
5. Clique em **Add**

### Via manifest.json

Adicione a seguinte linha ao arquivo `Packages/manifest.json` do seu projeto:

```json
{
  "dependencies": {
    "com.natteens.fynite": "https://github.com/Natteens/fynite.git#v0.7.0"
  }
}
```

Troque `v0.7.0` pela tag desejada ao atualizar. As tags publicadas estão em
[Releases](https://github.com/Natteens/fynite/releases).

## 🚀 Como usar

Cada peça tem uma responsabilidade só.

| Peça | Responsabilidade |
| --- | --- |
| **Context** | Os dados e serviços da entidade. Uma instância por máquina. |
| **State** | O comportamento enquanto aquele modo está ativo. |
| **Predicate** | Uma condição, sem efeitos colaterais. |
| **Transitions** | Quais States levam a quais, fora dos States. |

### 1. Context

Uma classe comum. Você a cria e passa para `Attach`.

```csharp
public sealed class PlayerContext
{
    public PlayerContext(PlayerInput input, PlayerMovement movement)
    {
        Input = input;
        Movement = movement;
    }

    public PlayerInput Input { get; }
    public PlayerMovement Movement { get; }
}
```

### 2. States

Um State é pequeno e sobrescreve só o que precisa. `Context`, `DeltaTime` e `FixedDeltaTime` são
propriedades protegidas — os callbacks não recebem parâmetro nenhum.

```csharp
public sealed class LocomotionState : FyniteState<PlayerContext>
{
    protected override void Update()
    {
        Context.Movement.Move(DeltaTime);
    }
}
```

Os callbacks disponíveis são `Enter`, `Update`, `FixedUpdate` e `Exit`. States concretos precisam de
construtor vazio e são instanciados uma única vez por máquina.

Um State não decide para onde ir, não alcança a máquina e não conhece os outros States.

### 3. Predicados

```csharp
public sealed class HasMovement : IPredicate<PlayerContext>
{
    public bool Evaluate(PlayerContext context) => context.Input.HasMovement;
}
```

Um predicado responde uma pergunta e não muda nada.

### 4. Módulos de transição

As transições ficam fora dos States, agrupadas por assunto.

```csharp
public sealed class LocomotionTransitions : IFyniteTransitions<PlayerContext>
{
    public void Configure(FyniteTransitions<PlayerContext> transitions)
    {
        transitions
            .From<IdleState>()
            .To<WalkState>()
            .When<HasMovement>();

        transitions
            .From<WalkState>()
            .To<IdleState>()
            .When<HasNoMovement>();
    }
}
```

Também dá para usar um lambda em vez de um tipo. Prefira `static` para não capturar nada:

```csharp
transitions
    .From<IdleState>()
    .To<WalkState>()
    .When(static context => context.Input.HasMovement);
```

E uma transição global, avaliada esteja a máquina onde estiver:

```csharp
transitions
    .Any()
    .To<DeadState>()
    .When<IsDead>();
```

Todo State citado em `Start<T>()`, `From<T>()` e `To<T>()` é registrado automaticamente. Não existe
`AddState<T>()`.

### 5. Attach

```csharp
using Fynite;
using UnityEngine;

public sealed class PlayerController : MonoBehaviour
{
    private FyniteMachine<PlayerContext> machine;

    private void Awake()
    {
        var context = new PlayerContext(input, movement);

        machine = Machine
            .Attach(this, context)
            .Start<LocomotionState>()
            .Use<LocomotionTransitions>()
            .Use<CombatTransitions>()
            .Build();
    }
}
```

Não existe classe `PlayerMachine` ou `EnemyMachine`: a composição acontece no ponto de criação.

A máquina é descartada automaticamente quando o owner é destruído — o controller não precisa de
`OnDestroy`. `Dispose()` explícito só é necessário quando a máquina deve ser encerrada antes da
destruição do owner. Guardar o field continua útil para consultar `IsRunning`, `CurrentStateType` e
`IsIn<TState>()`.

A máquina pública expõe só o necessário:

```csharp
bool IsRunning
Type CurrentStateType
bool IsIn<TState>()
void Dispose()
```

## ⏱ Lifecycle

**Build**

```text
cria os States → associa o Context → entra no State inicial → registra no PlayerLoop
```

**Update**

```text
avalia as transições globais
→ avalia as transições do State ativo
→ executa no máximo uma transição
→ executa o Update do State que ficou ativo
```

Quando ocorre transição, a ordem é `Exit` do atual, `Enter` do destino e `Update` do destino, tudo
no mesmo ciclo.

**FixedUpdate**

```text
FixedUpdate do State ativo
```

Transições não são avaliadas no FixedUpdate.

**Dispose**

```text
Exit do State ativo → remove do loop → marca como descartada
```

`Dispose()` é idempotente e roda `Exit` exatamente uma vez. Ele acontece sozinho quando o owner é
destruído; chamá-lo explicitamente só é necessário para encerrar a máquina antes disso.

## 🔁 Resolução de transições

Não existe prioridade numérica. A ordem é fixa:

1. transições globais (`Any()`) são avaliadas primeiro;
2. depois as transições do State ativo;
3. dentro do mesmo grupo vale a ordem de declaração, na ordem dos `Use<T>()`;
4. a primeira condição verdadeira vence;
5. no máximo uma transição por Update.

A avaliação faz short-circuit: assim que um predicado retorna `true`, a transição é escolhida e
**nenhum predicado seguinte do ciclo é avaliado** — nem no mesmo grupo, nem no grupo local quando uma
global vence. Isso vale igualmente no Editor, em Development Build e em Release; não existe opção
capaz de alterar essa semântica.

## 🎛 PlayerLoop

A máquina se registra sozinha ao ser construída. O sistema de Update é injetado logo depois de
`Update.ScriptRunBehaviourUpdate` e o de FixedUpdate logo depois de
`FixedUpdate.ScriptRunBehaviourFixedUpdate` — ou seja, os States enxergam o que os `MonoBehaviour`
fizeram no mesmo frame, e o `FixedUpdate` acontece antes da simulação de física.

A instalação é idempotente e funciona com Domain Reload ligado ou desligado. Os registros são
zerados em `SubsystemRegistration` e limpos ao sair do Play Mode.

A máquina para quando é descartada ou quando o owner é destruído. Se o owner for um `Behaviour`
desabilitado ou estiver num GameObject inativo, `Update` e `FixedUpdate` pausam sem executar `Exit`,
e ao reativar a máquina continua no mesmo State.

## ⚠️ Falhas

Se `Enter`, `Update`, `FixedUpdate`, `Exit` ou um predicado lançar exceção, a máquina não fica
parcialmente transitada: ela é marcada como faulted, sai do loop e para de executar callbacks. A
exceção não é escondida — durante o `Build()` ela é propagada, e durante um ciclo do PlayerLoop ela
é reportada no console. Uma máquina que falha não afeta as outras.

## ⚡ Alocações

Alocar durante `Attach`, `Build` e na criação de States, módulos e predicados é normal. No caminho
por frame — Update, avaliação de predicados, FixedUpdate e troca de State — não há alocação
intencional, nem LINQ, nem reflection.

## 📦 Sample

**Code First** — `Idle → Walk` e `Walk → Idle`, com Context, States, predicados, módulo de
transições e um controller sem `Update`, sem `FixedUpdate` e sem `OnDestroy`. Instale pelo Package
Manager, aba *Samples*.

## 🧭 Ainda não implementado

Esta versão é uma máquina plana. Hierarquia, superstates, initial child, active path, activities,
sequenciamento assíncrono, transições por evento, source generator e debugger visual ficam para
fases seguintes.

## 📝 Changelog

Veja o [CHANGELOG.md](CHANGELOG.md) para detalhes sobre mudanças e atualizações.

## 📄 Licença

Este projeto está licenciado sob a Licença MIT - veja o arquivo [LICENSE.md](LICENSE.md) para detalhes.
