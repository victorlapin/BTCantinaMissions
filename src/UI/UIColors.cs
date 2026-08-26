using System.Collections.Generic;
using BattleTech.UI;
using HBS;
using UnityEngine;

namespace BTCantinaMissions.UI
{
    /// <summary>Rich-text color tags sourced from the game's UIColorRefs at runtime —
    /// respects the active color preset (including colorblind variants), unlike
    /// hardcoded hex values. Hex strings are cached (colors load once at boot).</summary>
    public static class UIColors
    {
        private static readonly Dictionary<UIColor, string> hexCache = new Dictionary<UIColor, string>();

        /// <summary>Hex string like "#85DBF6FF" for a UIColor value.</summary>
        public static string Hex(UIColor color)
        {
            if (hexCache.TryGetValue(color, out var cached))
                return cached;

            var c = LazySingletonBehavior<UIManager>.Instance.UIColorRefs.GetUIColor(color);
            var hex = "#" + ColorUtility.ToHtmlStringRGBA(c);
            hexCache[color] = hex;
            return hex;
        }

        /// <summary>Wraps text into a rich-text color tag.</summary>
        public static string Wrap(string text, UIColor color)
        {
            return $"<color={Hex(color)}>{text}</color>";
        }
    }
}
