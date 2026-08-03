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

        private static int liveCount;
        private static bool ticking;
        private static bool needsCompaction;

        internal static int Count => liveCount;

        internal static void Register(IFyniteTickable machine)
        {
            if (machine.LoopSlot != Unregistered)
            {
                return;
            }

            liveCount++;

            if (ticking)
            {
                machine.LoopSlot = Pending;
                pending.Add(machine);
                return;
            }

            machine.LoopSlot = machines.Count;
            machines.Add(machine);
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

        internal static void Clear()
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
            ticking = false;
            needsCompaction = false;
        }

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
            if (ticking)
            {
                return;
            }

            ticking = true;

            try
            {
                for (var i = 0; i < machines.Count; i++)
                {
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
                ticking = false;
                Flush();
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
