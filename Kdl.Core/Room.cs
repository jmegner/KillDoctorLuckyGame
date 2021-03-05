using System.Collections.Immutable;

namespace Kdl.Core
{
    public record Room(
        int Id,
        string Name,
        ImmutableArray<int> Adjacent,
        ImmutableArray<int> Visible);
}
