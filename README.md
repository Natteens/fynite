# Fynite

Hierarchical State Machine pronta para integração rápida em projetos Unity.

## 📥 Instalação

Este pacote pode ser instalado através do Unity Package Manager usando a URL do Git.

**Sempre instale com uma tag.** Uma URL sem `#<tag>` resolve para o commit atual da branch
`main`, que é uma revisão de desenvolvimento: ela pode declarar o mesmo número de versão do
último release e ainda assim conter código diferente. Dois projetos instalados em dias
diferentes receberiam revisões distintas sob o mesmo `version`, e o Package Manager não avisa.

A última versão publicada é **`v0.5.0`**.

### Via Package Manager (Recomendado)

1. Abra o Package Manager (Window > Package Manager)
2. Clique no botão **+** no canto superior esquerdo
3. Selecione **"Add package from git URL..."**
4. Digite a URL: `https://github.com/Natteens/fynite.git#v0.5.0`
5. Clique em **Add**

### Via manifest.json

Adicione a seguinte linha ao arquivo `Packages/manifest.json` do seu projeto:

```json
{
  "dependencies": {
    "com.natteens.fynite": "https://github.com/Natteens/fynite.git#v0.5.0"
  }
}
```

Troque `v0.5.0` pela tag desejada ao atualizar. As tags publicadas estão em
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

A marcação **não é um checkbox no node**, e não é editável pelo canvas: ela é invariante de um pai —
um composto tem exatamente uma, uma folha não tem nenhuma — e não uma propriedade que cada filho ligue
por conta própria. Quem a define é a seção **State Authoring**, descrita abaixo.

### State Authoring (inspector do `.fyn`)

Selecione o `.fyn` na Project window. Abaixo de *Graph Settings* há uma seção de comandos que operam
sobre o graph real:

| Campo / botão | O que faz |
| --- | --- |
| **State** | Escolhe o estado que os comandos abaixo afetam. A lista mostra o caminho completo e a escolha é guardada por GUID, então renomear não troca a seleção. |
| **Path** / **Role** | Somente leitura: onde o estado está e o que ele é (`composite`/`leaf`, `initial child of …`, `no parent`). |
| **Name** + **Rename** | Renomeia. GUID, fios, reactions e posição ficam como estavam. |
| **Set as Initial** | Torna o estado o filho que o pai entra. Limpa a marca apenas dos irmãos diretos. |
| **Add Child** | Cria um estado dentro do selecionado. O primeiro filho de um pai vira o inicial; os seguintes, não. |
| **Duplicate** | Cria um irmão com identidade nova. A cópia nunca herda a marca de inicial. |
| **Move Under** + **Move** | Reparenta. A lista oferece só nodes que não formam ciclo, e nada é escolhido por você: sem seleção explícita o botão não roda. |
| **Unparented States** | Lista os estados ligados a pai nenhum e permite dar um pai a cada um. |

Cada comando bem-sucedido grava o `.fyn` e reimporta o arquivo, então o asset compilado logo abaixo
sempre descreve o graph como ele está agora.

Mover o filho inicial para fora de um pai que ainda tem outros filhos **não deixa o pai sem inicial**:
o sucessor é o primeiro filho restante na ordem em que o graph guarda seus nodes — a mesma ordem que o
compilador lê — e portanto é sempre o mesmo para o mesmo graph. Um pai que fica sem nenhum filho vira
folha e não precisa de inicial.

**O que essa seção não é.** Ela é um formulário no inspector, não um segundo editor: não desenha
nodes, edges, cards nem árvore, não mantém cópia do graph e não tem integração com o canvas. Abrir uma
não muda a seleção da outra, e o canvas precisa ser reaberto para mostrar edições feitas aqui. Ela
existe porque o menu de contexto, o pipeline de comandos e a seleção do Graph Toolkit não são
extensíveis por um package, então marcar um filho como inicial ou reparentar uma subárvore não têm
onde morar no canvas.

**Undo.** Esses comandos **não são desfazíveis**, e Fynite não declara que sejam.

As operações são gravadas com `UndoBeginRecordGraph`/`UndoEndRecordGraph`, a única API pública de
gravação do Graph Toolkit. Medido no Unity 6000.5.6f1 (`StateAuthoringUndoTests`), um
`Undo.PerformUndo()` depois de cada comando **não reverte nada**:

| Comando | O que o undo deveria desfazer | Resultado medido |
| --- | --- | --- |
| Add Child | remover o node criado | node continua no graph |
| Rename | restaurar o nome anterior | nome novo permanece |
| Set as Initial | devolver a marca ao irmão | marca nova permanece |
| Move | restaurar o fio de parent e as marcas | estado continua no destino |

Ou seja: o escopo de gravação do Graph Toolkit não cobre nem os campos serializados declarados por um
node derivado (nome, GUID, marca de inicial) nem as edições estruturais feitas dentro dele. Os testes
verificam **incondicionalmente** que um undo nunca deixa o graph incoerente — nenhum pai com dois
filhos iniciais, nenhum estado sem identidade — e isso continua valendo; o que não existe é reversão.

Na prática: **desfazer é reexecutar o comando inverso**. A superfície oferece todos eles (mover de
volta, renomear de volta, remarcar o inicial anterior), e cada comando grava o `.fyn`, então o
histórico real do arquivo é o seu controle de versão.

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
