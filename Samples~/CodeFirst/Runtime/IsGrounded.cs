using Fynite;

namespace FyniteSamples.CodeFirst
{
    public sealed class IsGrounded : IPredicate<ExampleContext>
    {
        public bool Evaluate(ExampleContext context) => context.Input.IsGrounded;
    }
}
