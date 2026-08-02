# Fynite

Hierarchical State Machine pronta para integração rápida em projetos Unity.

## 📥 Instalação

Este pacote pode ser instalado através do Unity Package Manager usando a URL do Git.

**Sempre instale com uma tag.** Uma URL sem `#<tag>` resolve para o commit atual da branch
`main`, que é uma revisão de desenvolvimento: ela pode declarar o mesmo número de versão do
último release e ainda assim conter código diferente. Dois projetos instalados em dias
diferentes receberiam revisões distintas sob o mesmo `version`, e o Package Manager não avisa.

A última versão publicada é **`v0.6.0`**.

### Via Package Manager (Recomendado)

1. Abra o Package Manager (Window > Package Manager)
2. Clique no botão **+** no canto superior esquerdo
3. Selecione **"Add package from git URL..."**
4. Digite a URL: `https://github.com/Natteens/fynite.git#v0.6.0`
5. Clique em **Add**

### Via manifest.json

Adicione a seguinte linha ao arquivo `Packages/manifest.json` do seu projeto:

```json
{
  "dependencies": {
    "com.natteens.fynite": "https://github.com/Natteens/fynite.git#v0.6.0"
  }
}
```

Troque `v0.6.0` pela tag desejada ao atualizar. As tags publicadas estão em
[Releases](https://github.com/Natteens/fynite/releases).

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
vem de um `FyniteDefinitionAsset` — o campo *Graph* do inspector, onde o próprio `.fyn` é aceito.

O contexto tem dois modos. Em **Auto**, o padrão, o inspector resolve o componente compatível **no
próprio GameObject** do runner e serializa essa referência em tempo de authoring; o campo fica em
somente leitura, e havendo mais de um candidato o inspector pede a escolha. Em **Override**, o campo
aceita um componente compatível de outro GameObject. Em nenhum dos dois há busca de cena, singleton
ou service locator: o que o player executa é sempre a referência já serializada.

### Authoring visual (`.fyn`)

Além da construção code-first, uma HFSM pode ser desenhada em um graph do **Unity Graph Toolkit**.

Crie um graph com **Assets → Create → Fynite → Fynite Graph** e abra o `.fyn` com duplo clique.

**O que existe no canvas**

| Node | Papel |
| --- | --- |
| **Root** | O topo da hierarquia, criado junto com o arquivo. Não tem pai, não executa blocos e não participa de reações — só entrega a porta `Children`. Um graph tem exatamente um. |
| **State** | Um estado. As actions de Enter/Tick/FixedTick/Exit são blocos empilhados dentro dele, e a ordem da pilha é a ordem de execução. |
| **Reaction** | Uma reação. Guards e effects são blocos empilhados dentro dela. |
| **Signal** | Um sinal declarado, com ou sem payload. |

O tipo de contexto **não é um node**. Ele é metadado do graph inteiro: selecione o `.fyn` na
Project window e escolha o tipo no inspector do importer, em *Graph Settings*, confirmando com
**Apply**. Enquanto nenhum tipo estiver escolhido o asset compilado existe mas não é executável, e
diz exatamente isso.

**Como as ligações funcionam**

- `Children` de um pai → `Parent` de cada filho monta a hierarquia. Como portas de entrada aceitam um
  único fio, um estado não consegue receber dois pais.
- `Reactions` de um estado → `Source` de uma reação declara a reação naquele estado.
- `Targeted By` do estado de destino → `Target` da reação faz a transição. **Deixar `Target`
  desconectado é o que torna a reação uma reação sem transição**: ela roda seus effects e o caminho
  ativo não muda.
- `Signal` de um sinal → `Signal` da reação escolhe o gatilho.

Cada tipo de porta é distinto, então o Graph Toolkit recusa ligações inválidas antes de você soltar o
fio.

**Filho inicial**

Todo estado composto precisa de exatamente um filho marcado como inicial; sem isso o compilador para
com `FYN0409` e diz qual estado não sabe para onde entrar. O estado marcado exibe `initial` no
subtítulo do node.

A marcação **não é um checkbox no node**: ela é invariante de um pai — um composto tem exatamente uma,
uma folha não tem nenhuma — e não uma propriedade que cada filho ligue por conta própria. Quem a mantém
são as operações de authoring `FyniteStateOperations`, que a corrigem a cada edição: o primeiro filho de
um pai vira o inicial, mover o inicial para fora elege deterministicamente um sucessor entre os irmãos
restantes, e um pai que fica sem filhos vira folha e não precisa de nenhum.

**Nome do estado**

Selecione o State no canvas e o **Graph Inspector** do Graph Toolkit mostra um campo `Name`, junto do
Subtitle. O default é `State`. Confirmar o campo atualiza o título do node na hora — sem selecionar o
`.fyn` na Project window, sem Apply/Revert e sem reimport.

O nome é só um rótulo: GUID, Parent, Children, reactions, blocks e posição ficam exatamente como
estavam. Renomear nunca quebra um fio nem uma referência externa.

O campo é *delayed*: ele aplica ao pressionar Enter ou ao sair do campo, não a cada tecla. Cada
mudança revalida o graph inteiro pelo compilador para pôr os diagnósticos nos nodes certos, e fazer
isso por caractere digitado deixaria a digitação pesada.

**Undo.** Renomear pelo Graph Inspector entra no Undo do Graph Toolkit, porque quem grava o valor é
uma node option dele. Já as operações de hierarquia chamadas por código (`CreateChild`, `MoveToParent`,
`SetAsInitial`, `Duplicate`) **não são desfazíveis**: medido no Unity 6000.5.6f1, o escopo de
`UndoBeginRecordGraph` não restaura nem os campos serializados declarados por um node derivado nem as
edições estruturais feitas dentro dele.

**Identidade**

Todo elemento carrega um GUID persistente. Renomear um estado ou um sinal nunca quebra uma ligação ou
uma referência externa, mover um node nunca altera a semântica, e copiar/colar gera identidades novas
para as cópias.

**Compilação**

Salvar o `.fyn` reimporta o arquivo, e o importer compila o graph para um `FyniteGraphAsset` — o
objeto principal do próprio arquivo. Arraste o `.fyn` direto para o campo *Graph* de um
`FyniteRunner`.

Se a compilação falhar, o asset fica deliberadamente vazio e guarda o erro: nada continua rodando uma
versão antiga em silêncio. Os diagnósticos aparecem no console, no inspector do asset e no node que os
causou.

**Referenciando sinais de fora do graph**

```csharp
[SerializeField] private FyniteSignalReference move;   // dropdown dos sinais do graph

private void OnJumpPressed() => runner.Raise(move);
```

A referência guarda o GUID do sinal, não seu nome nem seu índice: renomear o sinal não a quebra, e um
sinal apagado é detectado com uma mensagem clara em vez de resolver para o sinal vizinho. O GUID vira
um handle denso na primeira resolução e é reutilizado depois disso.

**Depuração em Play Mode**

**Window → Analysis → Fynite Debugger** mostra, para o `FyniteRunner` selecionado: o caminho ativo com
a folha destacada, o último sinal, a última reação, a última transição e a falha atual. Vários runners
do mesmo graph são observados separadamente. A janela só lê — nunca modifica o asset.

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
