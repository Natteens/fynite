# Code First

A small locomotion machine assembled entirely in code, showing every kind of rule side by side.
`Grounded` holds `Idle` and `Walk`, which swap on a **predicate**: is there movement input right
now? `Airborne` sits next to `Grounded` and is reached through an **event**: a jump was requested,
and later the character landed. `ActionState` sits next to both and runs an **activity**: a chain of
steps that takes half a second and then announces that it is done.

```text
GroundedState
├── IdleState   (initial child)
└── WalkState

AirborneState

ActionState
```

## Running it

1. Add `ExampleInput` and `ExampleController` to the same GameObject.
2. Assign the `ExampleInput` reference on the controller.
3. Enter play mode.

Change `Move` on `ExampleInput` from the inspector: away from zero switches to `Walk` and the
transform starts translating, back to zero returns to `Idle`.

For the air, right click the `ExampleInput` header and pick *Request jump* or *Land*. Both are
ordinary methods, so your own input code calls them the same way.

*Request action* leaves the ground branch for `ActionState`, and the console shows the chain running:
the action starts, half a second later it reaches its point, and the machine returns to `Grounded`
on its own.

## What to look at

`ExampleController` is the only file that knows how the machine is composed:

```csharp
machine = Machine
    .Attach(this, context)
    .Start<GroundedState>()
    .Child<GroundedState, IdleState>()
    .Child<GroundedState, WalkState>()
    .Use<LocomotionTransitions>()
    .Use<AirTransitions>()
    .Build();
```

It creates the context in `Awake`, declares the two children of `Grounded` and registers the two
transition modules. It has no `Update` and no `FixedUpdate`, because the PlayerLoop drives the
machine, and no `OnDestroy`, because destroying the owner shuts the machine down on its own. It also
never forwards the jump: `RequestJump` publishes, and the machine was already listening.

`ActionState` is the one state that does something over time:

```csharp
protected override void ConfigureActivity(FyniteActivityBuilder<ExampleContext> activity)
    => activity
        .Do(context => context.BeginAction())
        .Wait(0.5f)
        .Do(context => context.MarkActionPoint())
        .Publish(context => context.ActionFinished);
```

There is no coroutine and no timer field. The chain starts when the state is entered and is dropped
if the state is left early; its `Exit` still runs `CancelAction`, because undoing gameplay is not the
chain's job.

`ExampleContext` carries what the states need: the input component and the movement helpers. It is
created by the controller and belongs to that machine alone.

`IdleState`, `WalkState`, `GroundedState` and `AirborneState` each override only the callbacks they
use. None of them knows which state comes next.

`HasMovement` and `HasNoMovement` are the predicates — one question each, no side effects.

`LocomotionTransitions` routes `Idle` and `Walk` with those predicates. `AirTransitions` routes
`Grounded` and `Airborne` with `On(...)`, pointing straight at `JumpRequested` and `Landed`, and
`ActionTransitions` does the same for `ActionRequested` and `ActionFinished`. Because `Grounded` is
the parent, its rules to leave cover both children without being written twice.
