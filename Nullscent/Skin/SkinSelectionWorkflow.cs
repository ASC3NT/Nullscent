#nullable enable

using System;
using System.IO;
using System.Linq;
using Nullscent.Config;

namespace Nullscent.Skin;

/// <summary>
/// Shared user skin-selection transaction used by the game and DEBUG visual harness.
/// Validation, persistence, activation and post-activation invalidation stay on one path.
/// </summary>
internal static class SkinSelectionWorkflow
{
    public static bool TrySelect(
        SkinManager manager,
        SkinCatalog catalog,
        GameSettings settings,
        string? path,
        Func<bool> saveSettings,
        Action<string>? onActivated = null,
        Action<string>? showNotice = null)
    {
        SkinValidation? validation = path == null ? null : catalog.Validate(path);
        if (validation?.Status == SkinValidity.Invalid)
        {
            showNotice?.Invoke(validation.Summary);
            return false;
        }

        bool PersistSelection()
        {
            string? oldRoot = settings.SkinsRootDirectory;
            string? oldSelection = settings.SelectedSkinDirectory;
            settings.SkinsRootDirectory = catalog.Root;
            settings.SelectedSkinDirectory = path == null ? null : GetBuiltInSkinKey(catalog, path) ?? path;
            try
            {
                if (saveSettings()) return true;
            }
            catch
            {
                settings.SkinsRootDirectory = oldRoot;
                settings.SelectedSkinDirectory = oldSelection;
                throw;
            }

            settings.SkinsRootDirectory = oldRoot;
            settings.SelectedSkinDirectory = oldSelection;
            return false;
        }

        bool activated = validation == null
            ? manager.TryActivateInternal(PersistSelection)
            : manager.TryActivate(validation, PersistSelection);
        if (!activated)
        {
            showNotice?.Invoke(validation?.Errors.LastOrDefault()
                ?? "No se pudo activar la skin interna; se conservó la skin anterior.");
            return false;
        }

        onActivated?.Invoke(manager.CurrentSkinDirectory);
        if (validation?.Status == SkinValidity.Partial) showNotice?.Invoke(validation.Summary);
        else showNotice?.Invoke(path == null ? "Skin interna de Nullscent activa." : "Skin activada.");
        return true;
    }

    private static string? GetBuiltInSkinKey(SkinCatalog catalog, string directory)
    {
        if (catalog.BuiltInRoot == null) return null;
        string full = Path.GetFullPath(directory);
        SkinValidation? entry = catalog.Entries.FirstOrDefault(candidate => candidate.IsNative
            && string.Equals(candidate.Directory, full, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetDirectoryName(full), catalog.BuiltInRoot, StringComparison.OrdinalIgnoreCase));
        return entry == null ? null : $"builtin:{Path.GetFileName(full)}";
    }
}
