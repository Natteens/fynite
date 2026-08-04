[← Back to the README](../README.md)

# Hierarchy

States can contain other states. Declare the relationship where the machine is composed, and
everything else stays the same:

```csharp
machine = Machine
    .Attach(this, context)
    .Start<GroundedState>()
    .Child<GroundedState, LocomotionState>()
    .Child<GroundedState, AttackState>()
    .Use<PlayerTransitions>()
    .Build();
```

The first child declared is the one entered with its parent, so this machine starts in `Grounded`
and then in `Locomotion`. Use `InitialChild<TParent, TChild>()` only to override that convention.
`Start<T>()` still names the initial top-level state and cannot be a child.

While a child is active its parents are active too. `CurrentStateType` reports the deepest one and
`IsIn<T>()` answers `true` for any state on the path, so `IsIn<GroundedState>()` holds while
`AttackState` runs. `Enter`, `Update` and `FixedUpdate` run from the parent down; `Exit` runs from
the child up.

Transitions of the active child are evaluated before those of its parent, which is what makes
`Grounded → Airborne` apply to every child without being repeated. A transition into a state that
has children continues down to its initial child, and one that targets an active parent keeps that
parent running while restarting its branch.

Hierarchy is opt-in. A machine without a single `Child` call stays flat.

## Reading the active path

`CurrentStateType` answers for the deepest state. When you want the whole path — a HUD, a log line,
an inspector row — read it by index:

```csharp
for (var i = 0; i < machine.ActiveStateCount; i++)
{
    Debug.Log(machine.GetActiveStateType(i).Name);
}
```

For a machine sitting in `Grounded → Locomotion → Idle` that prints `Grounded`, `Locomotion`, `Idle`:
index 0 is the top level state and the last index is the current one, so
`GetActiveStateType(ActiveStateCount - 1)` is always `CurrentStateType`.

A flat machine has one entry. A machine that has stopped — disposed, owner destroyed, faulted, or
caught by a PlayerLoop reset — has none: `ActiveStateCount` is `0` and any index throws
`ArgumentOutOfRangeException`, which matches `CurrentStateType` being `null` and `IsIn<T>()` being
`false`.

There is no collection behind this. Both members read the path the machine already keeps, so looping
over it every frame allocates nothing.
