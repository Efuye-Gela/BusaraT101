using UnityEngine;

namespace Busara.Online.Client
{
    // One palette for the online client, sampled from the authored board, panel and frame art:
    // olive-slate surfaces, charcoal wells, an amber-to-orange edge and warm parchment text.
    public static class OnlineTheme
    {
        public static readonly Color Backdrop = Hex(0x1D1F1B);
        public static readonly Color Surface = Hex(0x3A403A);
        public static readonly Color SurfaceRaised = Hex(0x4B5249);
        public static readonly Color Well = Hex(0x2E2F2C);
        public static readonly Color Amber = Hex(0xFFB21A);
        public static readonly Color Ember = Hex(0xFF6A10);
        public static readonly Color Ink = Hex(0x1B1C19);
        public static readonly Color Text = Hex(0xF1ECDF);
        public static readonly Color TextMuted = Hex(0xB9B6A6);
        public static readonly Color Selected = Hex(0x6B5A2A);
        public static readonly Color Danger = Hex(0xE0664A);

        public const float Radius = 14f;
        public const float SlotSize = 84f;
        public const int TitleSize = 34;
        public const int HeadingSize = 28;
        public const int BodySize = 20;
        public const int ButtonSize = 20;
        public const int SmallSize = 16;

        public static readonly Vector2 Reference = new Vector2(1600, 1000);
        public static readonly Rect Content = new Rect(36, 104, 1528, 790);

        // Centres of the 4x4 wells in Board.png (1466x1474), as fractions of the art size.
        public static readonly float[] WellX = { .209f, .406f, .600f, .797f };
        public static readonly float[] WellY = { .193f, .397f, .600f, .803f };

        // Content-local layout (top-left origin) shared by the scene builder and the runtime controller.
        public const float SideWidth = 900f;
        public const float PanelGap = 30f;
        public const float PanelHeight = 730f;
        public const float TurnBarHeight = 76f;
        public const float SeatY = 88f;
        public const float SeatWidth = 488f;
        public const float SeatHeight = 646f;
        public const float ArtY = 132f;
        public const float ArtSize = 472f;
        public const float DetailsY = 612f;
        public const float ActionsX = 1006f;
        public const float ActionsWidth = 522f;

        public static float SeatX(int seat) => seat * (SeatWidth + 12f);
        public static float ArtX(int seat) => SeatX(seat) + (SeatWidth - ArtSize) / 2f;

        public static Vector2 SlotCentre(int slot)
        {
            int column = slot % 8, row = slot / 8;
            return new Vector2(ArtX(column / 4) + WellX[column % 4] * ArtSize, ArtY + WellY[row] * ArtSize);
        }

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);
    }
}
