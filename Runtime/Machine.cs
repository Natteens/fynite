using System;
using Object = UnityEngine.Object;

namespace Fynite
{
    public static class Machine
    {
        /// <summary>
        /// Starts configuring a machine owned by a Unity object. The machine stops when the owner is
        /// destroyed and pauses while it is disabled.
        /// </summary>
        public static FyniteMachineBuilder<TContext> Attach<TContext>(Object owner, TContext context)
            where TContext : class
        {
            if (owner == null)
            {
                throw new ArgumentNullException(
                    nameof(owner),
                    "Fynite: a machine needs a living Unity object as its owner.");
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new FyniteMachineBuilder<TContext>(owner, context);
        }
    }
}
