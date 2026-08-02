# Fynite

Hierarchical State Machine pronta para integração rápida em projetos Unity.

## 📥 Instalação

Este pacote pode ser instalado através do Unity Package Manager usando a URL do Git.

### Via Package Manager (Recomendado)

1. Abra o Package Manager (Window > Package Manager)
2. Clique no botão **+** no canto superior esquerdo
3. Selecione **"Add package from git URL..."**
4. Digite a URL: `https://github.com/Natteens/fynite.git`
5. Clique em **Add**

### Via manifest.json

Adicione a seguinte linha ao arquivo `Packages/manifest.json` do seu projeto:

```json
{
  "dependencies": {
    "com.natteens.fynite": "https://github.com/Natteens/fynite.git"
  }
}
```

## 🚀 Como Usar

O Fynite é uma HFSM determinística. Uma **definição** imutável descreve a árvore de estados, os
sinais, os guards, os efeitos e as reações. Cada **máquina** criada a partir dela tem seu próprio
caminho ativo, sua própria fila de sinais e suas próprias instâncias de bloco, então uma definição
pode ser compartilhada por dezenas de entidades.

Estados não exigem classes C#. O comportamento vem de **blocos** reutilizáveis encaixados nas fases
do estado:

```csharp
public sealed class ApplyMovement : IFyniteAction<IMovementContext>
{
    public void Execute(IMovementContext context, in FyniteExecution execution)
    {
        context.ApplyMovement(execution.DeltaTime);
    }
}
```

Como `TContext` é contravariante, esse bloco serve a qualquer contexto que implemente
`IMovementContext`.

### Construção code-first

```csharp
var builder = new FyniteBuilder<PlayerContext>();

var root     = builder.AddState("Root");
var grounded = builder.AddState("Grounded", root);
var idle     = builder.AddState("Idle", grounded);
var moving   = builder.AddState("Moving", grounded);

builder.SetRoot(root);
builder.SetInitial(root, grounded);
builder.SetInitial(grounded, idle);

var move = builder.AddSignal("Move");
var stop = builder.AddSignal("Stop");

builder.State(grounded).OnTick(() => new ApplyMovement());

builder.State(idle)
    .On(move)
    .When(() => new CanMove())
    .Do(() => new BeginMovement())
    .TransitionTo(moving)
    .Priority(0);

builder.State(moving).On(stop).TransitionTo(idle);

FyniteDefinition<PlayerContext> definition = builder.Build();
```

`Build()` valida a árvore inteira e falha com mensagens específicas (raiz ausente, ciclo de pais,
composto sem filho inicial, destino de outra definição, e assim por diante).

### Execução

```csharp
var machine = definition.CreateMachine(context);

machine.Start();          // entra da raiz até a folha inicial
machine.Raise(move);      // sinais são enfileirados, nunca reentrantes
machine.Tick(deltaTime);
machine.FixedTick(fixedDeltaTime);
machine.Stop();           // sai da folha até a raiz
machine.Dispose();
```

### Integração Unity

`FyniteRunner` é um `MonoBehaviour` que possui uma máquina e a dirige pelo player loop. A definição
vem de um `FyniteDefinitionAsset` e o contexto é atribuído explicitamente — nada é descoberto por
busca de cena, singleton ou service locator.

### Semânticas garantidas

- Um único caminho ativo; ancestrais comuns são preservados via LCA.
- Self-transition reinicia o estado e sua cadeia inicial.
- Reações são resolvidas por prioridade, depois profundidade, depois ordem de registro.
- Guards fazem short-circuit; uma reação rejeitada deixa outra candidata vencer.
- Sinais são FIFO e nunca interrompem a operação em andamento.
- Loops de sinal são interrompidos por um limite de microsteps.
- Exceções de blocos não são engolidas: a máquina vai para `Faulted` e a exceção é propagada.

O núcleo (`Fynite.Core`) é C# puro, não referencia a Unity e não é thread-safe: dirija cada máquina
a partir de uma única thread.

## 📝 Changelog

Veja o [CHANGELOG.md](CHANGELOG.md) para detalhes sobre mudanças e atualizações.

## 📄 Licença

Este projeto está licenciado sob a Licença MIT - veja o arquivo [LICENSE.md](LICENSE.md) para detalhes.
