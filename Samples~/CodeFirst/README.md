# Code First

A small locomotion machine assembled entirely in code. `Grounded` holds `Idle` and `Walk`: idle
switches to walk once there is movement input, and back again when the input stops. `Airborne` sits
next to `Grounded` and takes over whenever the character leaves the ground.

```text
GroundedState
├── IdleState   (initial child)
└── WalkState

AirborneState
```

## Running it

1. Add `ExampleInput` and `ExampleController` to the same GameObject.
2. Assign the `ExampleInput` reference on the controller.
3. Enter play mode and change `Move` and `IsGrounded` on `ExampleInput`, from the inspector or from
   your own input code.

Moving the `Move` vector away from zero switches to `Walk` and the transform starts translating;
clearing it goes back to `Idle`. Unchecking `IsGrounded` leaves the whole branch for `Airborne`.

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
machine, and no `OnDestroy`, because destroying the owner shuts the machine down on its own.

`ExampleContext` carries what the states need: the input component and the movement helpers. It is
created by the controller and belongs to that machine alone.

`IdleState`, `WalkState`, `GroundedState` and `AirborneState` each override only the callbacks they
use. None of them knows which state comes next.

`HasMovement`, `HasNoMovement`, `IsGrounded` and `IsAirborne` are the predicates — one question
each, no side effects.

`LocomotionTransitions` routes `Idle` and `Walk`, and `AirTransitions` routes `Grounded` and
`Airborne`. Because `Grounded` is the parent, its rule to leave for `Airborne` covers both children
without being written twice.
