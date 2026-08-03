using Fynite;

namespace FyniteSamples.CodeFirst
{
    public sealed class IsAirborne : IPredicate<ExampleContext>
    {
        public bool Evaluate(ExampleContext context) => !context.Input.IsGrounded;
    }
}
