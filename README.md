<div align="center">

# Fynite

**Write your states in C#. Fynite runs them from the Unity PlayerLoop.**

A code-first state machine for Unity 6, composed with generics and driven without a single
`machine.Update()` call.

[![Release workflow](https://github.com/Natteens/fynite/actions/workflows/release.yml/badge.svg)](https://github.com/Natteens/fynite/actions/workflows/release.yml)
[![Release](https://img.shields.io/github/v/release/Natteens/fynite?sort=semver&label=release&style=flat-square)](https://github.com/Natteens/fynite/releases)
[![Unity](https://img.shields.io/badge/Unity-6000.5%2B-000000?style=flat-square&logo=unity)](https://unity.com)
[![License](https://img.shields.io/github/license/Natteens/fynite?style=flat-square)](./LICENSE.md)

</div>

---

## Why Fynite?

A state machine usually costs you two things: a graph asset only the editor can review, and a
`machine.Update()` call every controller has to remember. Fynite drops both.

States, conditions and routing are ordinary classes named through generics, so a rename stays a
rename and a pull request stays readable. Transitions live in their own modules, which lets a state
be reordered or reused without touching it. `Build()` hands the machine to the PlayerLoop and the
owner's lifetime ends it.

Cost stays where you can see it: setup allocates, the per-frame path does not, and nothing anywhere
uses reflection or LINQ.

## Features

- **Compiler-checked composition** — states, predicates and transitions are classes named through generics.
- **PlayerLoop execution** — `Build()` registers the machine; destroying the owner stops it, disabling it pauses.
- **Events beside predicates** — `FyniteEvent` carries occurrences, predicates answer standing questions.
- **Activities** — a state runs a chain of timed steps, with no coroutine involved.
- **Optional hierarchy** — a parent state shares its transitions with every child.
- **Editor debugger** — *Tools > Fynite > Debugger* lists running machines and their active path.
- **No per-frame allocation** — the update path, `Publish()` and activity steps allocate nothing.

## Installation

Requires Unity **6000.5** or newer, and depends on no other package.

<!-- fynite-release:start -->
The latest published release is **v0.14.0**.

In the Unity Package Manager, choose *Add package from git URL* and paste:

```
https://github.com/Natteens/fynite.git#v0.14.0
```

Or declare the dependency in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.natteens.fynite": "https://github.com/Natteens/fynite.git#v0.14.0"
  }
}
```
<!-- fynite-release:end -->

Pin a tag. A URL without `#<tag>` follows `main`, which can carry the version number of the last
release and different code. Every tag is listed on the
[releases page](https://github.com/Natteens/fynite/releases).

## Quick Start

```csharp
using Fynite;
using UnityEngine;

public sealed class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerInput input;
    [SerializeField] private PlayerMovement movement;

    private void Awake()
    {
        Machine
            .Attach(this, new PlayerContext(input, movement))
            .Start<IdleState>()
            .Use<LocomotionTransitions>()
            .Build();
    }
}

public sealed class IdleState : FyniteState<PlayerContext>
{
    protected override void Enter() => Context.Movement.Stop();
}

public sealed class LocomotionTransitions : IFyniteTransitions<PlayerContext>
{
    public void Configure(FyniteTransitions<PlayerContext> transitions)
    {
        transitions
            .From<IdleState, WalkState>()
            .When<HasMovement>();

        transitions
            .From<WalkState, IdleState>()
            .When<HasNoMovement>();
    }
}
```

Every state named by `Start`, `From` or `To` is created once, while `Build()` runs. The controller
holds no reference and implements no `Update`, `FixedUpdate` or `OnDestroy`.

The **Code First** sample builds the same idea with a hierarchy, events and an activity. Import it
from the Package Manager, under the *Samples* tab.

## Documentation

- [Getting started](./docs/getting-started.md) — context, states, predicates and transition modules
- [Events](./docs/events.md) — occurrences that predicates cannot express
- [Activities](./docs/activities.md) — what a state does over time
- [Hierarchy](./docs/hierarchy.md) — nested states and reading the active path
- [Execution](./docs/execution.md) — PlayerLoop, transition order, shutdown and faults
- [Debugger](./docs/debugger.md) — the Editor window
- [API reference](./docs/api.md) — the public surface in one page
- [Sample walkthrough](./Samples~/CodeFirst/README.md) · [Changelog](./CHANGELOG.md)

## License

MIT. See [LICENSE.md](./LICENSE.md).
