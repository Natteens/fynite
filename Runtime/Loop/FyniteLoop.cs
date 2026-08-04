using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fynite
{
    internal static class FyniteLoop
    {
        internal const int Unregistered = -1;
        private const int Pending = -2;

        private static readonly List<IFyniteTickable> machines = new List<IFyniteTickable>(16);
        private static readonly List<IFyniteTickable> pending = new List<IFyniteTickable>(4);

        /// <summary>What a reset has to end. Filled once, when the reset starts.</summary>
        private static readonly List<IFyniteTickable> snapshotBuffer = new List<IFyniteTickable>(16);

        private static FyniteLoopPhase phase = FyniteLoopPhase.Idle;
        private static bool clearRequested;
        private static int liveCount;
        private static bool needsCompaction;

        /// <summary>
        /// Bumped the moment a reset is asked for. A build carries the value it started with, so a
        /// build the reset ran through is recognised and refused instead of registering into a loop
        /// that has already moved on.
        /// </summary>
        private static int generation;

        internal static int Count => liveCount;

        /// <summary>
        /// Opens a build and hands back the generation it belongs to. Refuses outright while the loop
        /// is shutting down, which is what makes a <c>Build()</c> from inside an <c>Exit</c> fail at
        /// the very first step instead of half-creating a machine nothing will ever own.
        /// </summary>
        internal static int BeginBuild()
        {
            if (phase == FyniteLoopPhase.ShuttingDown || clearRequested)
            {
                throw new InvalidOperationException(
                    "Fynite: a machine cannot be built while the PlayerLoop is shutting down. " +
                    "Building from a state's Exit during a reset is not supported.");
            }

            return generation;
        }

        /// <summary>True while <paramref name="buildGeneration"/> still describes the current loop.</summary>
        internal static bool IsCurrent(int buildGeneration)
            => phase != FyniteLoopPhase.ShuttingDown && buildGeneration == generation;

        /// <summary>
        /// Adds a finished machine to the loop, unless a reset happened while it was being built. A
        /// refused machine is the caller's to unwind: it was never registered, so nothing here holds it.
        /// </summary>
        internal static bool TryRegister(IFyniteTickable machine, int buildGeneration)
        {
            if (!IsCurrent(buildGeneration))
            {
                return false;
            }

            if (machine.LoopSlot != Unregistered)
            {
                return true;
            }

            liveCount++;

            if (phase == FyniteLoopPhase.Ticking)
            {
                machine.LoopSlot = Pending;
                pending.Add(machine);
                return true;
            }

            machine.LoopSlot = machines.Count;
            machines.Add(machine);
            return true;
        }

        internal static void Unregister(IFyniteTickable machine)
        {
            var slot = machine.LoopSlot;
            if (slot == Unregistered)
            {
                return;
            }

            machine.LoopSlot = Unregistered;

            if (slot == Pending)
            {
                if (pending.Remove(machine))
                {
                    liveCount--;
                }

                return;
            }

            if (slot < machines.Count && ReferenceEquals(machines[slot], machine))
            {
                machines[slot] = null;
                needsCompaction = true;
                liveCount--;
            }
        }

        /// <summary>
        /// Empties the loop and ends every machine still in it, so a subsystem reset, a rebuilt
        /// PlayerLoop or leaving play mode leaves nothing running and no event source holding a dead
        /// subscriber. Asked for from inside a tick, it lets the current callback finish first.
        /// </summary>
        internal static void Clear()
        {
            switch (phase)
            {
                case FyniteLoopPhase.ShuttingDown:
                    // Already resetting. The sweep in progress covers everything, and asking again
                    // must not start a second one.
                    return;

                case FyniteLoopPhase.Ticking:
                    if (!clearRequested)
                    {
                        // Invalidated now, not when the sweep runs, so a build that a callback is in
                        // the middle of is already stale by the time it tries to register.
                        generation++;
                        clearRequested = true;
                    }

                    return;

                default:
                    generation++;
                    PerformClear();
                    return;
            }
        }

        /// <summary>
        /// Snapshot, drop the registry, end the snapshot. Nothing can be added while this runs, so the
        /// snapshot is complete by construction and there is nothing to re-sweep.
        /// </summary>
        private static void PerformClear()
        {
            phase = FyniteLoopPhase.ShuttingDown;

            try
            {
                Snapshot();
                DropRegistry();

                for (var i = 0; i < snapshotBuffer.Count; i++)
                {
                    snapshotBuffer[i].LoopShutdown();
                }
            }
            finally
            {
                snapshotBuffer.Clear();
                DropRegistry();
                phase = FyniteLoopPhase.Idle;
            }
        }

        /// <summary>
        /// Copies what is registered, by instance identity, so a machine is never ended twice even if
        /// an inconsistency put it in both lists.
        /// </summary>
        private static void Snapshot()
        {
            snapshotBuffer.Clear();

            Snapshot(machines);
            Snapshot(pending);
        }

        private static void Snapshot(List<IFyniteTickable> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                var machine = source[i];
                if (machine == null || InSnapshot(machine))
                {
                    continue;
                }

                snapshotBuffer.Add(machine);
            }
        }

        private static bool InSnapshot(IFyniteTickable machine)
        {
            for (var i = 0; i < snapshotBuffer.Count; i++)
            {
                if (ReferenceEquals(snapshotBuffer[i], machine))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Forgets every registration without ending anything. Leaves the phase alone: only the method
        /// that owns the phase changes it.
        /// </summary>
        private static void DropRegistry()
        {
            for (var i = 0; i < machines.Count; i++)
            {
                if (machines[i] != null)
                {
                    machines[i].LoopSlot = Unregistered;
                }
            }

            for (var i = 0; i < pending.Count; i++)
            {
                pending[i].LoopSlot = Unregistered;
            }

            machines.Clear();
            pending.Clear();
            liveCount = 0;
            needsCompaction = false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Copies the machines the loop currently holds into a list the caller owns and reuses, for the
        /// Editor debugger. Stopped machines and the holes left by unregistering are skipped, and the
        /// loop's own lists never leave this class. A machine sits in exactly one of the two lists —
        /// <see cref="TryRegister"/> refuses a machine that already has a slot — so nothing can arrive
        /// twice.
        /// </summary>
        internal static void CollectDebugViews(List<IFyniteDebugView> destination)
        {
            destination.Clear();

            CollectDebugViews(machines, destination);
            CollectDebugViews(pending, destination);
        }

        private static void CollectDebugViews(
            List<IFyniteTickable> source,
            List<IFyniteDebugView> destination)
        {
            for (var i = 0; i < source.Count; i++)
            {
                if (source[i] is IFyniteDebugView view && view.DebugIsRunning)
                {
                    destination.Add(view);
                }
            }
        }
#endif

        internal static void RunUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return;
            }
#endif
            Tick(Time.deltaTime, false);
        }

        internal static void RunFixedUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return;
            }
#endif
            Tick(Time.fixedDeltaTime, true);
        }

        internal static void Tick(float delta, bool fixedStep)
        {
            if (phase != FyniteLoopPhase.Idle)
            {
                // A tick from inside a tick, or from inside a reset, is not a second frame.
                return;
            }

            phase = FyniteLoopPhase.Ticking;

            try
            {
                for (var i = 0; i < machines.Count; i++)
                {
                    if (clearRequested)
                    {
                        // The callback that asked for the reset has finished. Nothing else is updated
                        // in a loop that is about to be torn down.
                        break;
                    }

                    var machine = machines[i];
                    if (machine == null)
                    {
                        continue;
                    }

                    if (fixedStep)
                    {
                        machine.LoopFixedUpdate(delta);
                    }
                    else
                    {
                        machine.LoopUpdate(delta);
                    }
                }
            }
            finally
            {
                phase = FyniteLoopPhase.Idle;

                if (clearRequested)
                {
                    // The reset rebuilds the registry itself, including whatever was still pending, so
                    // the ordinary flush is skipped.
                    clearRequested = false;
                    PerformClear();
                }
                else
                {
                    Flush();
                }
            }
        }

        private static void Flush()
        {
            if (needsCompaction)
            {
                var write = 0;
                for (var read = 0; read < machines.Count; read++)
                {
                    var machine = machines[read];
                    if (machine == null)
                    {
                        continue;
                    }

                    machines[write] = machine;
                    machine.LoopSlot = write;
                    write++;
                }

                machines.RemoveRange(write, machines.Count - write);
                needsCompaction = false;
            }

            if (pending.Count == 0)
            {
                return;
            }

            for (var i = 0; i < pending.Count; i++)
            {
                var machine = pending[i];
                if (machine.LoopSlot != Pending)
                {
                    continue;
                }

                machine.LoopSlot = machines.Count;
                machines.Add(machine);
            }

            pending.Clear();
        }
    }
}
