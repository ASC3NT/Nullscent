#nullable enable

using System;
using Nullscent.Config;

namespace Nullscent.Skin;

/// <summary>
/// Applies the HD asset preference only after both active and internal skin providers
/// have been rebuilt successfully.
/// </summary>
internal static class HighResolutionSkinWorkflow
{
    public static bool TrySet(SkinManager manager, GameSettings settings, bool target,
        Action<bool> publishSetting, Func<bool> saveSettings)
    {
        bool previous = settings.HighResolutionSkins;
        if (previous == target && manager.HighResolutionSkinAssets == target)
            return true;

        bool targetPersisted = false;
        bool activated;
        try
        {
            activated = manager.TrySetHighResolutionSkinAssets(target, () =>
            {
                bool persisted = TryPersistSetting(settings, target, publishSetting, saveSettings);
                targetPersisted |= persisted;
                return persisted;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HighResolutionSkinWorkflow] Could not apply the HD asset preference: {ex.Message}");
            activated = false;
        }

        if (activated)
            return true;

        if (targetPersisted)
        {
            try
            {
                if (!TryPersistSetting(settings, previous, publishSetting, saveSettings))
                    Console.WriteLine("[HighResolutionSkinWorkflow] Could not persist rollback of the HD asset preference.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HighResolutionSkinWorkflow] Could not persist rollback of the HD asset preference: {ex.Message}");
            }
        }
        else
        {
            settings.HighResolutionSkins = previous;
            try { publishSetting(previous); }
            catch (Exception ex)
            {
                Console.WriteLine($"[HighResolutionSkinWorkflow] Could not restore the published HD asset preference: {ex.Message}");
            }
        }

        return false;
    }

    internal static bool TryPersistSetting(GameSettings settings, bool target,
        Action<bool> publishSetting, Func<bool> saveSettings)
    {
        bool previous = settings.HighResolutionSkins;
        if (previous == target)
            return true;

        try
        {
            settings.HighResolutionSkins = target;
            publishSetting(target);
            if (saveSettings())
                return true;
        }
        catch
        {
            RestorePrevious();
            throw;
        }

        RestorePrevious();
        return false;

        void RestorePrevious()
        {
            settings.HighResolutionSkins = previous;
            publishSetting(previous);
        }
    }
}
