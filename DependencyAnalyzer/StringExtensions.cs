using System;
using System.Collections.Generic;
using System.Linq;

namespace DependencyAnalyzer
{
    static class StringExtensions
    {
        public static bool ContainsAny(this string input, IEnumerable<string> patterns,
            StringComparison comparisonType = StringComparison.Ordinal)
        {
            return patterns.Any(p => input.IndexOf(p, comparisonType) >= 0);
        }
    }
}
