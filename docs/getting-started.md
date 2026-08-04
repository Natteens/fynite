[← Back to the README](../README.md)

# Getting started

A machine is made of five pieces, and four of them are classes you write.

The **context** is an ordinary class holding the data and services an entity needs. You create it
and pass it to `Attach`, and every state of that machine reads it through the protected `Context`
property.

A **state** is the behavior that runs while a mode is active. It overrides `Enter`, `Update`,
`FixedUpdate` or `Exit`, and only the ones it uses. `Context`, `DeltaTime` and `FixedDeltaTime` are
protected properties, so the callbacks take no parameters:

```csharp
public sealed class WalkState : FyniteState<PlayerContext>
{
    protected override void Update()
    {
        Context.Movement.Move(Context.Input.Move * DeltaTime);
    }
}
```

A state knows nothing about the machine or about the other states, which keeps it reusable.

A **predicate** answers one question and changes nothing. Implement `IPredicate<TContext>` when the
condition deserves a name:

```csharp
public sealed class HasMovement : IPredicate<PlayerContext>
{
    public bool Evaluate(PlayerContext context) => context.Input.HasMovement;
}
```

Pass a lambda to `When` when it does not deserve one, and combine conditions with plain C#:

```csharp
transitions
    .From<IdleState, WalkState>()
    .When(context => context.Input.HasMovement && context.Movement.IsGrounded);
```

A **transition module** implements `IFyniteTransitions<TContext>` and groups related rules by
subject. Registering several with `Use<T>()` composes a machine from locomotion, combat or damage
rules written independently.

The **owner** is the Unity object passed to `Attach`. It controls the lifetime of the machine, so no
`PlayerMachine` or `EnemyMachine` class is needed.

## Declaring transitions

`From<TFrom, TTo>()` names both ends of a rule in one call. The explicit form builds exactly the
same transition, and is worth reaching for when the two states deserve their own lines:

```csharp
transitions
    .From<IdleState>()
    .To<WalkState>()
    .When<HasMovement>();
```

`Any<TTo>()` and `Any().To<TTo>()` do the same for a rule that applies to every state.

Every state named by `Start<T>()`, `From<T>()` or `To<T>()` is registered automatically, and each
one is instantiated exactly once per machine.

## Where to go next

Predicates cover the persistent questions — `HasMovement`, `IsGrounded`, `IsDead`. For the things
that happen once, see [Events](./events.md). For what a state does over time, see
[Activities](./activities.md).
