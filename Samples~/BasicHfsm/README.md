# Basic HFSM

A real `.fyn` graph, compiled by the Fynite importer and executed by a `FyniteRunner`.

## What it contains

```
Root
├── Idle (initial)
└── Moving
```

| Signal   | Payload  |
| -------- | -------- |
| `Move`   | none     |
| `Stop`   | none     |
| `Notify` | `string` |

| Reaction              | Result                                            |
| --------------------- | ------------------------------------------------- |
| `Idle` + `Move`       | → `Moving`, guarded by `CanMoveGuard`, logs an effect |
| `Moving` + `Stop`     | → `Idle`                                          |
| `Moving` + `Notify`   | no transition; the effect reads the payload       |

Blocks used: `LogAction` on enter and exit of both states (four occurrences, each with its own
configured message), `AdvanceAction` on tick, `FixedStepAction` on fixed tick, `CanMoveGuard` as a
guard and `NoteAction` as the effect of the reaction without a target.

## Trying it

1. Import the sample from the Package Manager.
2. Open `Patrol.fyn` to see the graph.
3. Create an empty GameObject and add **Patrol Context**, **Fynite Runner** and **Patrol Driver**.
4. Drag `Patrol.fyn` onto the runner's *Graph* field and the Patrol Context onto its *Context* field.
5. Assign the three signals on the driver — each is a dropdown of the graph's signals.
6. Enter play mode and press <kbd>1</kbd> (Move), <kbd>2</kbd> (Stop) and <kbd>3</kbd> (Notify).

Open **Window → Analysis → Fynite Debugger** with the runner selected to watch the active path.

`PatrolContext.trace` records what ran, in order, so the effect of every signal is visible.

## Regenerating the graph

`Assets → Create → Fynite → Samples → Regenerate Patrol Sample Graph` rebuilds `Patrol.fyn` from
`PatrolGraphGenerator`, which is also a worked example of building a graph from code.
