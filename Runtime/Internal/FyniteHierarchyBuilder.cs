using System;
using System.Collections.Generic;
using System.Text;

namespace Fynite
{
    internal sealed class FyniteHierarchyBuilder
    {
        private readonly List<HierarchyRelation> relations = new List<HierarchyRelation>();
        private readonly List<InitialChildOverride> overrides = new List<InitialChildOverride>();

        /// <summary>Finds the relation a child already has, which is the one that can conflict.</summary>
        private readonly Dictionary<int, int> relationByChild = new Dictionary<int, int>();

        internal void Relate(int parentIndex, Type parentType, int childIndex, Type childType)
        {
            if (parentIndex == childIndex)
            {
                throw new InvalidOperationException(
                    $"Fynite: '{childType.Name}' cannot be a child of itself.");
            }

            if (relationByChild.TryGetValue(childIndex, out var slot))
            {
                var declared = relations[slot];
                if (declared.Parent == parentIndex)
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Fynite: '{childType.Name}' cannot be a child of both " +
                    $"'{declared.ParentType.Name}' and '{parentType.Name}'.");
            }

            relationByChild.Add(childIndex, relations.Count);
            relations.Add(new HierarchyRelation(parentIndex, parentType, childIndex));
        }

        internal void SetInitialChild(int parentIndex, int childIndex)
            => overrides.Add(new InitialChildOverride(parentIndex, childIndex));

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

            for (var i = 0; i < relations.Count; i++)
            {
                var relation = relations[i];
                var owner = Validate(relation.Parent, stateCount);
                var child = Validate(relation.Child, stateCount);

                parent[child] = owner;

                // The first child declared is the one a composite state falls into.
                if (initialChild[owner] < 0)
                {
                    initialChild[owner] = child;
                }
            }

            for (var i = 0; i < overrides.Count; i++)
            {
                var declared = overrides[i];
                var owner = Validate(declared.Parent, stateCount);
                var child = Validate(declared.Child, stateCount);

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

        /// <summary>
        /// One declared parent-child pair. The parent's type travels with it because the "two parents"
        /// diagnostic has to name the parent that was declared first, long after the fact.
        /// </summary>
        private readonly struct HierarchyRelation
        {
            internal readonly int Parent;
            internal readonly Type ParentType;
            internal readonly int Child;

            internal HierarchyRelation(int parent, Type parentType, int child)
            {
                Parent = parent;
                ParentType = parentType;
                Child = child;
            }
        }

        private readonly struct InitialChildOverride
        {
            internal readonly int Parent;
            internal readonly int Child;

            internal InitialChildOverride(int parent, int child)
            {
                Parent = parent;
                Child = child;
            }
        }
    }
}
