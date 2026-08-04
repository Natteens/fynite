[← Back to the README](../README.md)

# Events

A predicate answers *is this true right now?*. Some things are not like that: taking damage,
finishing an attack, an animation reaching its end. Those happened, once, and asking about them a
frame later makes no sense. `FyniteEvent` covers them.

The system that owns the occurrence publishes it, and nothing else changes:

```csharp
public sealed class PlayerHealth
{
    public FyniteEvent Damaged { get; } = new();

    public DamageResult LastDamage { get; private set; }

    public void ApplyDamage(DamageResult damage)
    {
        LastDamage = damage;
        Damaged.Publish();
    }
}
```

The transition module decides that the occurrence changes state, by pointing at the event through
the context:

```csharp
transitions
    .Any<PlayerStateHit>()
    .On(context => context.Health.Damaged);
```

The lambda does not have to be `static`. It runs exactly once, during `Build()`, and the machine
keeps the instance it returned.

The controller stays out of it. It builds the machine and stops there, with nothing to forward, no
handler to register and nothing to unregister:

```csharp
machine = Machine
    .Attach(this, context)
    .Start<PlayerStateLocomotion>()
    .Use<PlayerDamageTransitions>()
    .Build();
```

## Subscription

Subscribing happens during `Build()`. Unsubscribing happens on its own: when you call `Dispose()`,
when the owner is destroyed, or if the machine faults. After that, publishing reaches nothing and
throws nothing.

Two transitions listening to the same event subscribe once. Publishing with nobody listening does
nothing at all.

## Resolution

`Publish()` does not change state where it is called. It records the occurrence, and the machine
resolves it at the start of its next update, so publishing from `Enter`, `Update`, `Exit`, a
predicate or `FixedUpdate` never re-enters the machine.

Pending events are resolved before predicates are evaluated, and at most one transition runs per
update. An event nobody is listening for in the states currently active is spent rather than kept
waiting for some later state.

Publishing the same event several times before it is resolved counts once, so a hundred `Damaged`
calls in one frame cause one transition. Different events keep the order they were published in.

See [Execution](./execution.md) for how events and predicates share an update.
