#nullable enable

using System;
using System.Globalization;

namespace Nullscent.Skin
{
    /// <summary>Pure precedence and orientation rules shared by gameplay and tests.</summary>
    public static class ManiaSkinRules
    {
        public static bool ResolveDownScroll(bool userDownScroll, bool useSkinPreference, bool? skinUpsideDown) =>
            useSkinPreference && skinUpsideDown.HasValue ? !skinUpsideDown.Value : userDownScroll;

        public static bool FlipNote(ManiaKeymodeDefinition profile, int columnIndex, string part, bool downScroll)
        {
            bool flip = true;
            if (profile.SupportsFlipOverrides)
            {
                string normalizedPart = part.ToUpperInvariant();
                flip = profile.NoteFlips.TryGetValue($"{columnIndex}{normalizedPart}", out bool partFlip) ? partFlip
                    : profile.NoteFlips.TryGetValue(columnIndex.ToString(CultureInfo.InvariantCulture), out bool columnFlip) ? columnFlip
                    : profile.NoteFlipWhenUpsideDown ?? true;
            }

            return part.Equals("T", StringComparison.OrdinalIgnoreCase) ? downScroll && flip : !downScroll && flip;
        }

        public static bool FlipKey(ManiaKeymodeDefinition profile, int columnIndex, bool pressed, bool downScroll)
        {
            if (downScroll) return false;
            if (!profile.SupportsFlipOverrides) return true;

            string column = columnIndex.ToString(CultureInfo.InvariantCulture);
            return profile.KeyFlips.TryGetValue(pressed ? $"{column}D" : column, out bool stateFlip) ? stateFlip
                : profile.KeyFlips.TryGetValue(column, out bool columnFlip) ? columnFlip
                : profile.KeyFlipWhenUpsideDown ?? true;
        }
    }
}
