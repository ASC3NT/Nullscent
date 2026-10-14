#nullable enable

using System;
using Nullscent.Config;

namespace Nullscent.Skin;

/// <summary>
/// Shared profile-setting transaction used by Game1 and the DEBUG visual harness.
/// </summary>
internal static class LegacySkinCompatibilityWorkflow
{
    public static bool TrySet(
        SkinManager manager,
        SkinCatalog catalog,
        GameSettings settings,
        LegacySkinCompatibilityMode targetMode,
        Action<LegacySkinCompatibilityMode> publishSetting,
        Func<bool> saveSettings)
    {
        if (!Enum.IsDefined(targetMode)) return false;
        if (settings.LegacySkinCompatibilityMode == targetMode
            && manager.LegacyCompatibilityMode == targetMode
            && catalog.CompatibilityMode == targetMode)
            return true;

        bool PersistMode() => TryPersistSetting(settings, targetMode, publishSetting, saveSettings);
        bool activated;
        if (manager.LegacyCompatibilityMode == targetMode)
        {
            try { activated = PersistMode(); }
            catch (Exception ex)
            {
                Console.WriteLine($"[LegacySkinCompatibilityWorkflow] Could not persist the selected profile: {ex.Message}");
                return false;
            }
        }
        else
        {
            activated = manager.TrySetLegacyCompatibilityMode(targetMode, PersistMode);
        }
        if (!activated) return false;

        catalog.SetCompatibilityMode(targetMode);
        return true;
    }

    internal static bool TryPersistSetting(
        GameSettings settings,
        LegacySkinCompatibilityMode targetMode,
        Action<LegacySkinCompatibilityMode> publishSetting,
        Func<bool> saveSettings)
    {
        LegacySkinCompatibilityMode previousMode = settings.LegacySkinCompatibilityMode;
        if (previousMode == targetMode) return true;

        try
        {
            publishSetting(targetMode);
            if (saveSettings()) return true;
        }
        catch
        {
            RestorePreviousMode();
            throw;
        }

        RestorePreviousMode();
        return false;

        void RestorePreviousMode()
        {
            try
            {
                publishSetting(previousMode);
            }
            finally
            {
                settings.LegacySkinCompatibilityMode = previousMode;
            }
        }
    }
}
