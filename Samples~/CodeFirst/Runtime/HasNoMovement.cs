using Fynite;

namespace FyniteSamples.CodeFirst
{
    public sealed class HasNoMovement : IPredicate<ExampleContext>
    {
        public bool Evaluate(ExampleContext context) => !context.Input.HasMovement;
    }
}
