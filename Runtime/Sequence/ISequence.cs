using System.Threading;
using System.Threading.Tasks;

namespace Fynite
{
    public interface ISequence
    {
        bool IsDone { get; }
        void Start();
        bool Update();
    }

    public delegate Task PhaseStep(CancellationToken ct);
}