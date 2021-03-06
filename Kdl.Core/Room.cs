using System.Collections.Immutable;

namespace Kdl.Core
{
    public record Room(
        int Id,
        string Name,
        ImmutableArray<int> Adjacent,
        ImmutableArray<int> Visible)
    {
        public override string ToString()
        {
            var adjacentText = string.Join(',', Adjacent);
            var visibleText = string.Join(',', Visible);
            return $"{Id};{Name};A:{adjacentText};V:{visibleText}";
        }
    }
}
