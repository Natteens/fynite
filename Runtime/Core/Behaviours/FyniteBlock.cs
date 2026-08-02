using System;

namespace Fynite
{
    /// <summary>
    /// Base of every configurable behaviour block that a compiled graph can instantiate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A compiled graph stores one <em>prototype</em> per authored occurrence: the object carrying the
    /// configuration the user typed into the editor. Each machine needs its own instance so that two
    /// entities sharing a definition never share a block's mutable state, and that instance has to be
    /// produced without <c>Activator.CreateInstance</c>, <c>Type.GetType</c> or any other reflective
    /// construction at runtime.
    /// </para>
    /// <para>
    /// <see cref="CreateBlockInstance"/> solves that with <see cref="object.MemberwiseClone"/>: the
    /// prototype copies itself. Cloning is shallow, so configuration should be value-typed or otherwise
    /// immutable. A block that owns a reference-typed field it mutates must override
    /// <see cref="CreateBlockInstance"/> and deep-copy that field.
    /// </para>
    /// <para>
    /// Derive from <see cref="FyniteAction{TContext}"/> or <see cref="FyniteGuard{TContext}"/> rather
    /// than from this type directly.
    /// </para>
    /// </remarks>
    [Serializable]
    public abstract class FyniteBlock
    {
        /// <summary>
        /// Produces the instance a single machine will use, copied from this prototype.
        /// </summary>
        /// <remarks>
        /// The default implementation returns a shallow copy. Override to deep-copy mutable
        /// reference-typed configuration, and always start from <c>base.CreateBlockInstance()</c> so the
        /// copy keeps this prototype's concrete type.
        /// </remarks>
        public virtual FyniteBlock CreateBlockInstance() => (FyniteBlock)MemberwiseClone();
    }

    /// <summary>
    /// Base for an authored action block: state enter/tick/fixed-tick/exit work, or a reaction effect.
    /// </summary>
    /// <remarks>
    /// A subclass is a plain serializable class, so its fields become the block's configuration in the
    /// graph editor. Because <typeparamref name="TContext"/> is contravariant on
    /// <see cref="IFyniteAction{TContext}"/>, writing a block against a narrow contract makes it usable
    /// by every graph whose context implements that contract.
    /// </remarks>
    /// <typeparam name="TContext">Context contract this block requires.</typeparam>
    [Serializable]
    public abstract class FyniteAction<TContext> : FyniteBlock, IFyniteAction<TContext> where TContext : class
    {
        /// <inheritdoc />
        public abstract void Execute(TContext context, in FyniteExecution execution);
    }

    /// <summary>
    /// Base for an authored guard block.
    /// </summary>
    /// <remarks>
    /// Guards must be pure predicates with respect to the machine; see <see cref="IFyniteGuard{TContext}"/>.
    /// </remarks>
    /// <typeparam name="TContext">Context contract this guard requires.</typeparam>
    [Serializable]
    public abstract class FyniteGuard<TContext> : FyniteBlock, IFyniteGuard<TContext> where TContext : class
    {
        /// <inheritdoc />
        public abstract bool Evaluate(TContext context, in FyniteExecution execution);
    }
}
