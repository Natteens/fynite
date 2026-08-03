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

A máquina é plana por padrão. Quando precisar de superstates, acrescente `Child<TParent, TChild>()`
à mesma composição — veja [Hierarquia](#6-hierarquia-opcional).

## 📥 Instalação

Este pacote pode ser instalado através do Unity Package Manager usando a URL do Git.

**Sempre instale com uma tag.** Uma URL sem `#<tag>` resolve para o commit atual da branch
`main`, que é uma revisão de desenvolvimento: ela pode declarar o mesmo número de versão do
último release e ainda assim conter código diferente. Dois projetos instalados em dias
diferentes receberiam revisões distintas sob o mesmo `version`, e o Package Manager não avisa.

Escolha uma tag publicada na página [Releases](https://github.com/Natteens/fynite/releases) e
acrescente `#<tag>` à URL.

### Via Package Manager (Recomendado)

1. Abra o Package Manager (Window > Package Manager)
2. Clique no botão **+** no canto superior esquerdo
3. Selecione **"Add package from git URL..."**
4. Digite a URL: `https://github.com/Natteens/fynite.git#<published-tag>`
5. Clique em **Add**

### Via manifest.json

Adicione a seguinte linha ao arquivo `Packages/manifest.json` do seu projeto:

```json
{
  "dependencies": {
    "com.natteens.fynite": "https://github.com/Natteens/fynite.git#<published-tag>"
  }
}
```

Troque a tag pela desejada ao atualizar.

## 🚀 Como usar

Cada peça tem uma responsabilidade só.

| Peça | Responsabilidade |
| --- | --- |
| **Context** | Os dados e serviços da entidade. Uma instância por máquina. |
| **State** | O comportamento enquanto aquele modo está ativo. Pode agrupar outros States. |
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

### 6. Hierarquia (opcional)

Tudo acima é uma FSM plana e continua funcionando sem mudar nada. Quando um State precisa agrupar
outros, declare os filhos no mesmo lugar em que a máquina é montada:

```csharp
machine = Machine
    .Attach(this, context)
    .Start<GroundedState>()
    .Child<GroundedState, LocomotionState>()
    .Child<GroundedState, AttackState>()
    .Use<PlayerTransitions>()
    .Build();
```

Em linguagem simples:

- um State sem filhos funciona como um State de FSM normal;
- um State com filhos vira superstate: enquanto ele estiver ativo, um dos seus filhos também está;
- **o primeiro filho declarado é o inicial** — aqui, `LocomotionState`;
- `InitialChild<TParent, TChild>()` só é necessário para sobrescrever essa convenção;
- `Start<T>()` continua sendo o State inicial de topo e não pode ser um filho;
- não existe Root: States sem pai são States de topo.

O que muda em tempo de execução:

- `CurrentStateType` mostra o **leaf**, o State ativo mais profundo;
- `IsIn<T>()` responde `true` para qualquer State ativo, inclusive os pais;
- as transições do leaf são verificadas antes das do pai, que são verificadas antes das do avô;
- `Enter` acontece do pai para o filho;
- `Exit` acontece do filho para o pai;
- `Update` e `FixedUpdate` acontecem do pai para o filho.

Isso permite comportamento específico no filho, regras compartilhadas no superstate e regras
universais em `Any()`, sem repetir transição nenhuma.

```csharp
public sealed class PlayerTransitions : IFyniteTransitions<PlayerContext>
{
    public void Configure(FyniteTransitions<PlayerContext> transitions)
    {
        transitions.From<LocomotionState>().To<AttackState>().When<PressedAttack>();
        transitions.From<AttackState>().To<GroundedState>().When<AttackFinished>();
        transitions.From<GroundedState>().To<AirborneState>().When<LeftTheGround>();
    }
}
```

`AttackState → GroundedState` volta para o filho inicial de `Grounded` sem sair de `Grounded`; já
`GroundedState → AirborneState` vale estando em `Locomotion` ou em `Attack`, porque é uma transição
do pai.

## ⏱ Lifecycle

Numa máquina plana o caminho ativo tem um State só, e tudo abaixo se reduz ao comportamento de
sempre.

**Build**

```text
cria os States → associa o Context → entra no State inicial de topo
→ entra nos filhos iniciais → registra no PlayerLoop
```

**Update**

```text
avalia as transições globais
→ avalia as transições do leaf, depois as do pai, subindo até o topo
→ executa no máximo uma transição
→ executa o Update dos States ativos, do topo para o leaf
```

Quando ocorre transição, sai-se do leaf para cima só até o ancestral comum, entra-se de lá para
baixo até o novo leaf, e o `Update` do novo caminho roda no mesmo ciclo.

**FixedUpdate**

```text
FixedUpdate dos States ativos, do topo para o leaf
```

Transições não são avaliadas no FixedUpdate.

**Dispose**

```text
Exit do leaf → Exit dos ancestrais até o topo → remove do loop → marca como descartada
```

`Dispose()` é idempotente e roda `Exit` exatamente uma vez por State ativo. Ele acontece sozinho
quando o owner é destruído; chamá-lo explicitamente só é necessário para encerrar a máquina antes
disso.

## 🔁 Resolução de transições

Não existe prioridade numérica. A ordem é fixa:

1. transições globais (`Any()`) são avaliadas primeiro;
2. depois as transições do leaf ativo;
3. depois as do pai, e assim por diante até o State de topo;
4. dentro do mesmo grupo vale a ordem de declaração, na ordem dos `Use<T>()`;
5. a primeira condição verdadeira vence;
6. no máximo uma transição por Update.

A avaliação faz short-circuit: assim que um predicado retorna `true`, a transição é escolhida e
**nenhum predicado seguinte do ciclo é avaliado** — nem no mesmo grupo, nem no grupo local quando uma
global vence. Isso vale igualmente no Editor, em Development Build e em Release; não existe opção
capaz de alterar essa semântica.

Quando o destino tem filhos, a máquina desce automaticamente até o filho inicial. Quando o destino
já é um ancestral ativo, o ancestral continua ativo e a ramificação recomeça pelo filho inicial. E
uma transição explícita de um State para ele mesmo reinicia esse State — se ele for composto,
reinicia a ramificação inteira.

## 🎛 PlayerLoop

A máquina se registra sozinha ao ser construída. O sistema de Update é injetado logo depois de
`Update.ScriptRunBehaviourUpdate` e o de FixedUpdate logo depois de
`FixedUpdate.ScriptRunBehaviourFixedUpdate` — ou seja, os States enxergam o que os `MonoBehaviour`
fizeram no mesmo frame, e o `FixedUpdate` acontece antes da simulação de física.

A instalação é idempotente e funciona com Domain Reload ligado ou desligado. Os registros são
zerados em `SubsystemRegistration` e limpos ao sair do Play Mode.

A máquina para quando é descartada ou quando o owner é destruído. Se o owner for um `Behaviour`
desabilitado ou estiver num GameObject inativo, `Update` e `FixedUpdate` pausam sem executar `Exit`,
e ao reativar a máquina continua no mesmo caminho ativo.

## ⚠️ Falhas

Se `Enter`, `Update`, `FixedUpdate`, `Exit` ou um predicado lançar exceção — em qualquer nível do
caminho ativo, pai ou filho — a máquina não fica parcialmente transitada: ela é marcada como
faulted, sai do loop e para de executar callbacks, sem repetir a exceção a cada frame. A
exceção não é escondida — durante o `Build()` ela é propagada, e durante um ciclo do PlayerLoop ela
é reportada no console. Uma máquina que falha não afeta as outras.

## ⚡ Alocações

Alocar durante `Attach`, `Build` e na criação de States, módulos e predicados é normal. No caminho
por frame — Update, avaliação de predicados, FixedUpdate, troca de State, cálculo do ancestral comum,
descida até o filho inicial e `IsIn<T>()` — não há alocação intencional, nem LINQ, nem reflection. O
caminho ativo vive em buffers criados uma única vez, no `Build()`.

## 📦 Sample

**Code First** — `Grounded > Idle | Walk` mais um `Airborne` de topo, com Context, States,
predicados, dois módulos de transições e um controller sem `Update`, sem `FixedUpdate` e sem
`OnDestroy`. Instale pelo Package Manager, aba *Samples*.

## 🧭 Ainda não implementado

Activities, sequenciamento assíncrono, transições por evento, transições internal/local
configuráveis, source generator e debugger visual ficam para fases seguintes.

## 📝 Changelog

Veja o [CHANGELOG.md](CHANGELOG.md) para detalhes sobre mudanças e atualizações.

## 📄 Licença

Este projeto está licenciado sob a Licença MIT - veja o arquivo [LICENSE.md](LICENSE.md) para detalhes.
