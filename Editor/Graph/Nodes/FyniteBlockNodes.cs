using System;
using Fynite.Authoring;
using Unity.GraphToolkit.Editor;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// An action block stacked inside a state, running in one of the four state phases.
    /// </summary>
    /// <remarks>
    /// There is one node type per phase rather than one node with a phase dropdown. The phase then reads
    /// off the node's own title on the canvas, the creation menu offers the four phases directly, and a
    /// block cannot silently change when it runs because someone brushed a dropdown.
    /// </remarks>
    [Serializable]
    public abstract class FyniteActionBlockNode : FyniteBlockNodeBase
    {
        /// <inheritdoc />
        public sealed override FyniteBlockKind BlockKind => FyniteBlockKind.Action;

        /// <summary>Which phase of the owning state this block runs in.</summary>
        public abstract FynitePhase Phase { get; }
    }

    /// <summary>An action that runs when the state is entered.</summary>
    [Serializable]
    [Node("Fynite/Actions/On Enter")]
    [UseWithGraph(typeof(FyniteGraph))]
    [UseWithContext(typeof(FyniteStateNode))]
    public sealed class FyniteEnterActionNode : FyniteActionBlockNode
    {
        /// <inheritdoc />
        public override FynitePhase Phase => FynitePhase.Enter;

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            Title = "On Enter";
        }
    }

    /// <summary>An action that runs every tick while the state is active.</summary>
    [Serializable]
    [Node("Fynite/Actions/On Tick")]
    [UseWithGraph(typeof(FyniteGraph))]
    [UseWithContext(typeof(FyniteStateNode))]
    public sealed class FyniteTickActionNode : FyniteActionBlockNode
    {
        /// <inheritdoc />
        public override FynitePhase Phase => FynitePhase.Tick;

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            Title = "On Tick";
        }
    }

    /// <summary>An action that runs every fixed tick while the state is active.</summary>
    [Serializable]
    [Node("Fynite/Actions/On Fixed Tick")]
    [UseWithGraph(typeof(FyniteGraph))]
    [UseWithContext(typeof(FyniteStateNode))]
    public sealed class FyniteFixedTickActionNode : FyniteActionBlockNode
    {
        /// <inheritdoc />
        public override FynitePhase Phase => FynitePhase.FixedTick;

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            Title = "On Fixed Tick";
        }
    }

    /// <summary>An action that runs when the state is exited.</summary>
    [Serializable]
    [Node("Fynite/Actions/On Exit")]
    [UseWithGraph(typeof(FyniteGraph))]
    [UseWithContext(typeof(FyniteStateNode))]
    public sealed class FyniteExitActionNode : FyniteActionBlockNode
    {
        /// <inheritdoc />
        public override FynitePhase Phase => FynitePhase.Exit;

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            Title = "On Exit";
        }
    }

    /// <summary>A guard stacked inside a reaction, deciding whether it may run.</summary>
    [Serializable]
    [Node("Fynite/Guard")]
    [UseWithGraph(typeof(FyniteGraph))]
    [UseWithContext(typeof(FyniteReactionNode))]
    public sealed class FyniteGuardBlockNode : FyniteBlockNodeBase
    {
        /// <inheritdoc />
        public override FyniteBlockKind BlockKind => FyniteBlockKind.Guard;

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            Title = "Guard";
        }
    }

    /// <summary>An effect stacked inside a reaction, running between the exits and the entries.</summary>
    [Serializable]
    [Node("Fynite/Effect")]
    [UseWithGraph(typeof(FyniteGraph))]
    [UseWithContext(typeof(FyniteReactionNode))]
    public sealed class FyniteEffectBlockNode : FyniteBlockNodeBase
    {
        /// <inheritdoc />
        public override FyniteBlockKind BlockKind => FyniteBlockKind.Action;

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            Title = "Effect";
        }
    }
}
