using System;
using TMPro;

namespace Wynnyng.Presentation
{
    public static class TmpResources
    {
        public const string MissingResourcesMessage =
            "Recursos essenciais do TextMeshPro ausentes. Pare o Play e execute " +
            "Window > TextMeshPro > Import TMP Essential Resources. Aguarde a importacao " +
            "e execute Wynnyng > Preparar Fases 0 e 1A antes de apertar Play novamente.";

        /// <summary>Optional loaders allow tests to simulate missing resources without deleting user assets.</summary>
        public static bool TryGetDefaultFont(out TMP_FontAsset font,
            Func<bool> settingsAvailable = null, Func<TMP_FontAsset> loadFont = null)
        {
            font = null;
            // LoadDefaultSettings is null-safe and does not open the TMP importer during Play.
            bool hasSettings = settingsAvailable == null
                ? TMP_Settings.LoadDefaultSettings() != null : settingsAvailable();
            if (!hasSettings) return false;
            // The property dereferences TMP_Settings.instance, so it must only run after the guard.
            font = loadFont == null ? TMP_Settings.defaultFontAsset : loadFont();
            return font != null;
        }

        public static TMP_FontAsset RequireDefaultFont()
        {
            if (!TryGetDefaultFont(out TMP_FontAsset font))
                throw new InvalidOperationException(MissingResourcesMessage);
            return font;
        }
    }
}
