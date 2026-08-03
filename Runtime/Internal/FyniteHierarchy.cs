namespace Fynite
{
    internal sealed class FyniteHierarchy
    {
        internal readonly int[] Parent;
        internal readonly int[] InitialChild;
        internal readonly int[] Depth;
        internal readonly int PathCapacity;

        internal FyniteHierarchy(int[] parent, int[] initialChild, int[] depth, int pathCapacity)
        {
            Parent = parent;
            InitialChild = initialChild;
            Depth = depth;
            PathCapacity = pathCapacity;
        }
    }
}
