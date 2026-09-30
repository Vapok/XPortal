using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace XPortalNetworks.UI
{
    internal static class UIFonts
    {
        private const string AveriaSansFontName = "Valheim-AveriaSansLibre";
        private const string LiberationSansName = "LiberationSans";
        private const string AveriaFallbackToken = "Averia";

        private static TMP_FontAsset _averiaSansFont;

        public static TMP_FontAsset GetAveriaSansFont()
        {
            if (_averiaSansFont != null)
            {
                return _averiaSansFont;
            }

            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < fonts.Length; i++)
            {
                TMP_FontAsset candidate = fonts[i];
                if (candidate != null && candidate.name == AveriaSansFontName)
                {
                    _averiaSansFont = candidate;
                    break;
                }
            }

            if (_averiaSansFont == null)
            {
                for (int i = 0; i < fonts.Length; i++)
                {
                    TMP_FontAsset candidate = fonts[i];
                    if (candidate != null && candidate.name.IndexOf(AveriaFallbackToken, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _averiaSansFont = candidate;
                        break;
                    }
                }
            }

            if (_averiaSansFont != null)
            {
                EnsureDefaultFont(_averiaSansFont);
            }

            return _averiaSansFont;
        }

        public static void EnsureDefaultFont(TMP_FontAsset font)
        {
            if (font == null)
            {
                return;
            }

            if (TMP_Settings.defaultFontAsset == null ||
                TMP_Settings.defaultFontAsset.name.IndexOf(LiberationSansName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TMP_Settings.defaultFontAsset = font;
            }
        }
    }
}
