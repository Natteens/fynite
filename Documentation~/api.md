[← Back to the README](../README.md)

# API reference

The whole public surface of the package.

`FyniteEvent` is the one type you create — `new FyniteEvent()` — because the occurrence belongs to
whatever publishes it. Everything else is handed to you: `Machine.Attach` returns the builder, the
builder returns the machine, `Configure` receives the `FyniteTransitions<TContext>` of that machine,
and `ConfigureActivity` receives the `FyniteActivityBuilder<TContext>` of that state. None of them
has a public constructor, and the two fluent structs are only valid when a `FyniteTransitions`
handed them out.

## Composition

```csharp
Machine.Attach<TContext>(Object owner, TContext context)

FyniteMachineBuilder<TContext>.Start<TState>()
FyniteMachineBuilder<TContext>.Child<TParent, TChild>()
FyniteMachineBuilder<TContext>.InitialChild<TParent, TChild>()
FyniteMachineBuilder<TContext>.Use<TTransitions>()
FyniteMachineBuilder<TContext>.Build()
```

## Machine

```csharp
FyniteMachine<TContext>.IsRunning
FyniteMachine<TContext>.CurrentStateType
FyniteMachine<TContext>.ActiveStateCount
FyniteMachine<TContext>.GetActiveStateType(int index)
FyniteMachine<TContext>.IsIn<TState>()
FyniteMachine<TContext>.Dispose()
```

## States

```csharp
FyniteState<TContext>.Context
FyniteState<TContext>.DeltaTime
FyniteState<TContext>.FixedDeltaTime
FyniteState<TContext>.Enter()
FyniteState<TContext>.Update()
FyniteState<TContext>.FixedUpdate()
FyniteState<TContext>.Exit()
FyniteState<TContext>.ConfigureActivity(FyniteActivityBuilder<TContext>)

IPredicate<TContext>.Evaluate(TContext context)
IFyniteTransitions<TContext>.Configure(FyniteTransitions<TContext> transitions)
```

## Activities

```csharp
FyniteActivityBuilder<TContext>.Do(context => ...)
FyniteActivityBuilder<TContext>.Wait(seconds)
FyniteActivityBuilder<TContext>.WaitUntil(context => ...)
FyniteActivityBuilder<TContext>.WaitFor(context => ...)
FyniteActivityBuilder<TContext>.Publish(context => ...)
```

## Transitions and events

```csharp
FyniteTransitions<TContext>.From<TFrom, TTo>()
FyniteTransitions<TContext>.Any<TTo>()
FyniteTransitions<TContext>.From<TState>()
FyniteTransitions<TContext>.Any()

FyniteTransitionSource<TContext>.To<TState>()

FyniteTransitionTarget<TContext>.When<TPredicate>()
FyniteTransitionTarget<TContext>.When(context => ...)
FyniteTransitionTarget<TContext>.On(context => ...)

new FyniteEvent()
FyniteEvent.Publish()
```

`From<TFrom, TTo>()` and `Any<TTo>()` hand back a target, ready for the condition. `From<TState>()`
and `Any()` hand back a source, which names the other end with `To<TState>()` and reaches the same
target. Both are structs you chain through rather than types you store.

## Allocation

Allocating during `Attach`, `Build` and while creating states, modules and predicates is expected —
that is where the event subscriptions, their queue and the activity chains are set up. The per-frame
path, `Publish()` and every activity step included, allocates nothing intentionally, and uses no
LINQ and no reflection.
