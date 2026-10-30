using UnityEngine;

namespace Busara.Online.Client
{
    // Runtime state colours for the online client. OnlineMVP is authored with the Panther Court kit, so text uses
    // its gold and parchment, and image tints multiply over the kit's sprites rather than replace their colour.
    public static class OnlineTheme
    {
        public static readonly Color Gold = Hex(0xE8C27A);
        public static readonly Color Text = Hex(0xF4EFE6);
        public static readonly Color TextMuted = Hex(0xB8AD9A);

        // A chosen option or a ready seat warms toward gold; the theme's selection ring shows on a chosen slot.
        public static readonly Color Selected = Hex(0xFFC768);
        public static readonly Color Unselected = Color.white;
        public static readonly Color SlotChosen = Color.white;
        public static readonly Color SlotIdle = Hex(0xFFFFFF, 0f);

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);
    }
}
