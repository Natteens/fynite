[← Back to the README](../README.md)

# Activities

Some states are not one moment: an attack begins, lands a hit partway through and then ends. A state
can declare that as a chain of steps, and Fynite runs it. This is optional — a state that leaves
`ConfigureActivity` alone behaves exactly as it would without the feature.

```csharp
public sealed class AttackState : FyniteState<PlayerContext>
{
    protected override void ConfigureActivity(FyniteActivityBuilder<PlayerContext> activity)
    {
        activity
            .Do(context => context.Attack.Begin())
            .WaitFor(context => context.Attack.HitFrame)
            .Do(context => context.Attack.ApplyHit())
            .WaitFor(context => context.Attack.Finished)
            .Publish(context => context.AttackCompleted);
    }

    protected override void Exit()
    {
        Context.Attack.Cancel();
    }
}
```

## The five steps

- `Do` runs something once and moves on in the same tick.
- `Wait` holds for a number of seconds of `DeltaTime`; `Wait(0)` passes straight through.
- `WaitUntil` holds until a condition answers true, asking it at most once per update.
- `WaitFor` holds until an event happens, listening only from the moment the step is reached.
- `Publish` publishes an event once and moves on.

Steps that do not block chain together within a single update, so `Do → Do → Publish` is one tick.

## Lifetime

The chain starts when the state is entered and is cancelled when it is left, with nothing to
remember and nothing to dispose. Entering the state again starts it over from the first step: a wait
counts its full duration again, and a `WaitFor` listens again. Reaching the last step leaves the
chain finished; it never loops on its own.

Cancelling stops the remaining steps and stops listening. It does not undo anything, so gameplay
cleanup stays in `Exit` — the callback that runs whether the chain finished or was cut short.

Activities run on `Update`, never on `FixedUpdate`. A disabled owner freezes them where they are: no
step runs, no time passes and no condition is asked until the owner comes back. No coroutine, `Task`
or thread is involved — a chain is an array of steps and a cursor, compiled by `Build()`.

In a hierarchy each state on the active path may have its own chain, and they run parent first. A
parent keeps its chain while its children come and go; only the states a transition actually removes
are cancelled.

## Leaving the state

Where the machine goes next is not the state's decision. `Publish` announces that something
happened, and a transition module decides what that means:

```csharp
transitions
    .From<AttackState, LocomotionState>()
    .On(context => context.AttackCompleted);
```

That transition resolves on a later update, like any other event, so a chain never switches state
from inside its own step.
