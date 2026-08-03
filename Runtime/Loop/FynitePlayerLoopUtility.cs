using System;
using UnityEngine.LowLevel;

namespace Fynite
{
    internal static class FynitePlayerLoopUtility
    {
        internal static bool Remove(ref PlayerLoopSystem parent, Type systemType)
        {
            var children = parent.subSystemList;
            if (children == null)
            {
                return false;
            }

            var found = false;
            var kept = 0;

            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].type == systemType)
                {
                    found = true;
                    continue;
                }

                found |= Remove(ref children[i], systemType);
                children[kept++] = children[i];
            }

            if (kept != children.Length)
            {
                var trimmed = new PlayerLoopSystem[kept];
                Array.Copy(children, trimmed, kept);
                parent.subSystemList = trimmed;
            }

            return found;
        }

        internal static bool InsertAfter(ref PlayerLoopSystem parent, Type anchor, PlayerLoopSystem system)
        {
            var children = parent.subSystemList;
            if (children == null)
            {
                return false;
            }

            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].type == anchor)
                {
                    parent.subSystemList = Insert(children, i + 1, system);
                    return true;
                }
            }

            for (var i = 0; i < children.Length; i++)
            {
                if (InsertAfter(ref children[i], anchor, system))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool AppendTo(ref PlayerLoopSystem parent, Type phase, PlayerLoopSystem system)
        {
            var children = parent.subSystemList;
            if (children == null)
            {
                return false;
            }

            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].type == phase)
                {
                    var target = children[i].subSystemList ?? Array.Empty<PlayerLoopSystem>();
                    children[i].subSystemList = Insert(target, target.Length, system);
                    return true;
                }

                if (AppendTo(ref children[i], phase, system))
                {
                    return true;
                }
            }

            return false;
        }

        internal static int Count(PlayerLoopSystem parent, Type systemType)
        {
            var children = parent.subSystemList;
            if (children == null)
            {
                return 0;
            }

            var total = 0;
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].type == systemType)
                {
                    total++;
                }

                total += Count(children[i], systemType);
            }

            return total;
        }

        private static PlayerLoopSystem[] Insert(PlayerLoopSystem[] source, int index, PlayerLoopSystem system)
        {
            var result = new PlayerLoopSystem[source.Length + 1];
            Array.Copy(source, 0, result, 0, index);
            result[index] = system;
            Array.Copy(source, index, result, index + 1, source.Length - index);
            return result;
        }
    }
}
