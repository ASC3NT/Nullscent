#nullable enable

using System;
using System.Collections.Generic;

namespace Nullscent.Skin
{
    /// <summary>
    /// Resolves semantic alternatives inside one provider before consulting the next provider.
    /// A non-null value, including an intentional transparent asset, is considered resolved.
    /// </summary>
    internal static class SkinAssetCascade
    {
        public static T? Resolve<T>(
            Func<string, T?> resolveActive,
            IEnumerable<string?> activeCandidates,
            Func<string, T?> resolveFallback,
            IEnumerable<string?> fallbackCandidates)
            where T : class
        {
            T? active = FirstResolved(resolveActive, activeCandidates);
            return active ?? FirstResolved(resolveFallback, fallbackCandidates);
        }

        public static T? FirstResolved<T>(Func<string, T?> resolveFromProvider, IEnumerable<string?> candidates)
            where T : class
        {
            foreach (string? candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                T? resolved = resolveFromProvider(candidate);
                if (resolved != null) return resolved;
            }

            return null;
        }
    }
}
