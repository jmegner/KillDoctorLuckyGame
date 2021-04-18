using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Util
{
    public static class IEnumerableExtensions
    {
        private static readonly Random _random = new();

        public static T RandomChoice<T>(this ICollection<T> source, Random random = null)
        {
            var i = (random ?? _random).Next(source.Count);
            return source.ElementAt(i);
        }

        static IEnumerable<IEnumerable<T>> CartesianProduct<T>(this IEnumerable<IEnumerable<T>> sequences)
        {
            IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
            return sequences.Aggregate(
                emptyProduct,
                (accumulator, sequence) =>
                    from accseq in accumulator
                    from item in sequence
                    select accseq.Concat(new[] { item })
                );
        }

        public static T MaxElementBy<T>(this IEnumerable<T> source, Func<T, double> selector)
        {
            var currentMaxElement = default(T);
            var currentMaxValue = double.MinValue;

            foreach (var element in source)
            {
                var value = selector(element);
                if (currentMaxValue < value)
                {
                    currentMaxValue = value;
                    currentMaxElement = element;
                }
            }

            return currentMaxElement;
        }
    }
}
