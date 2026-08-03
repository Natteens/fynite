using System;
using System.Collections.Generic;
using System.Text;

namespace Fynite
{
    internal sealed class FyniteHierarchyBuilder
    {
        private readonly List<int> relationParent = new List<int>();
        private readonly List<Type> relationParentType = new List<Type>();
        private readonly List<int> relationChild = new List<int>();
        private readonly Dictionary<int, int> relationByChild = new Dictionary<int, int>();

        private readonly List<int> overrideParent = new List<int>();
        private readonly List<int> overrideChild = new List<int>();

        internal void Relate(int parentIndex, Type parentType, int childIndex, Type childType)
        {
            if (parentIndex == childIndex)
            {
                throw new InvalidOperationException(
                    $"Fynite: '{childType.Name}' cannot be a child of itself.");
            }

            if (relationByChild.TryGetValue(childIndex, out var slot))
            {
                if (relationParent[slot] == parentIndex)
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Fynite: '{childType.Name}' cannot be a child of both " +
                    $"'{relationParentType[slot].Name}' and '{parentType.Name}'.");
            }

            relationByChild.Add(childIndex, relationChild.Count);
            relationParent.Add(parentIndex);
            relationParentType.Add(parentType);
            relationChild.Add(childIndex);
        }

        internal void SetInitialChild(int parentIndex, int childIndex)
        {
            overrideParent.Add(parentIndex);
            overrideChild.Add(childIndex);
        }

        internal FyniteHierarchy Compile(int stateCount, Type[] stateTypes)
        {
            var parent = new int[stateCount];
            var initialChild = new int[stateCount];
            var depth = new int[stateCount];

            for (var i = 0; i < stateCount; i++)
            {
                parent[i] = -1;
                initialChild[i] = -1;
            }

            for (var i = 0; i < relationChild.Count; i++)
            {
                var owner = Validate(relationParent[i], stateCount);
                var child = Validate(relationChild[i], stateCount);

                parent[child] = owner;

                if (initialChild[owner] < 0)
                {
                    initialChild[owner] = child;
                }
            }

            for (var i = 0; i < overrideChild.Count; i++)
            {
                var owner = Validate(overrideParent[i], stateCount);
                var child = Validate(overrideChild[i], stateCount);

                if (parent[child] != owner)
                {
                    throw new InvalidOperationException(
                        $"Fynite: InitialChild<{stateTypes[owner].Name}, {stateTypes[child].Name}>() is " +
                        $"invalid because '{stateTypes[child].Name}' is not a direct child of " +
                        $"'{stateTypes[owner].Name}'.");
                }

                initialChild[owner] = child;
            }

            ResolveDepth(parent, depth, stateTypes);

            var pathCapacity = 1;
            for (var i = 0; i < stateCount; i++)
            {
                if (depth[i] >= pathCapacity)
                {
                    pathCapacity = depth[i] + 1;
                }
            }

            return new FyniteHierarchy(parent, initialChild, depth, pathCapacity);
        }

        private static int Validate(int index, int stateCount)
        {
            if (index < 0 || index >= stateCount)
            {
                throw new InvalidOperationException(
                    "Fynite: the hierarchy references a state that does not belong to this machine.");
            }

            return index;
        }

        private static void ResolveDepth(int[] parent, int[] depth, Type[] stateTypes)
        {
            var count = parent.Length;
            var chain = count == 0 ? Array.Empty<int>() : new int[count];

            for (var i = 0; i < count; i++)
            {
                depth[i] = -1;
            }

            for (var i = 0; i < count; i++)
            {
                if (depth[i] >= 0)
                {
                    continue;
                }

                var length = 0;
                var node = i;

                while (node >= 0 && depth[node] < 0)
                {
                    if (length == count)
                    {
                        throw new InvalidOperationException(
                            "Fynite: hierarchy cycle detected: " +
                            DescribeCycle(parent, stateTypes, i) + ".");
                    }

                    chain[length++] = node;
                    node = parent[node];
                }

                var level = node < 0 ? -1 : depth[node];
                for (var k = length - 1; k >= 0; k--)
                {
                    depth[chain[k]] = ++level;
                }
            }
        }

        private static string DescribeCycle(int[] parent, Type[] stateTypes, int start)
        {
            var node = start;
            for (var i = 0; i < parent.Length; i++)
            {
                node = parent[node];
            }

            var entry = node;
            var text = new StringBuilder(stateTypes[entry].Name);

            node = parent[entry];
            while (node != entry)
            {
                text.Append(" -> ").Append(stateTypes[node].Name);
                node = parent[node];
            }

            return text.Append(" -> ").Append(stateTypes[entry].Name).ToString();
        }
    }
}
