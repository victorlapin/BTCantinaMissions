using BattleTech.UI;
using HBS;
using UnityEngine;

namespace BTCantinaMissions.UI
{
    /// <summary>Rich-text color tags sourced from the game's UIColorRefs at runtime —
    /// respects the active color preset (including colorblind variants), unlike
    /// hardcoded hex values.</summary>
    public static class UIColors
    {
        /// <summary>Hex string like "#85DBF6FF" for a UIColor value.</summary>
        public static string Hex(UIColor color)
        {
            var c = LazySingletonBehavior<UIManager>.Instance.UIColorRefs.GetUIColor(color);
            return "#" + ColorUtility.ToHtmlStringRGBA(c);
        }

        /// <summary>Wraps text into a rich-text color tag.</summary>
        public static string Wrap(string text, UIColor color)
        {
            return $"<color={Hex(color)}>{text}</color>";
        }
    }
}
