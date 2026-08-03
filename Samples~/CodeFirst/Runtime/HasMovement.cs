using Fynite;

namespace FyniteSamples.CodeFirst
{
    public sealed class HasMovement : IPredicate<ExampleContext>
    {
        public bool Evaluate(ExampleContext context) => context.Input.HasMovement;
    }
}
