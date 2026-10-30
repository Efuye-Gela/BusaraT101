using Busara.Online.Client;
using Eg.Editor;
using Eg.UI;
using Eg.UI.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BusaraOnlineUiKit;

// Deterministically authors OnlineMVP: camera, event system, Eg UIRoot with Entry/Lobby/Match pages, header,
// footer and the wired BusaraOnline controller. Run through BusaraOnlineBuild.GenerateScene.
public static class BusaraOnlineSceneBuilder
{
    private const string ArtRoot = "Assets/Busara/Sprites/";

    public static void Populate()
    {
        var round = RoundSprite();
        var circle = CircleSprite();
        var body = Font("Poppins_500");
        var bold = Font("Poppins_900");
        var mono = Font("Share Tech_400");
        var buttonPrefab = ButtonPrefab(round, body);
        var labelPrefab = LabelPrefab(body);

        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = OnlineTheme.Backdrop;
        camera.cullingMask = 0;
        EgUIBuilder.EnsureEventSystem();

        var canvas = new GameObject("UI Root", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UIRoot));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = OnlineTheme.Reference;
        scaler.matchWidthOrHeight = .5f;
        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)backdrop.transform);
        backdrop.GetComponent<Image>().color = OnlineTheme.Backdrop;
        backdrop.GetComponent<Image>().raycastTarget = false;

        var connection = Header(canvas.transform, round, bold, mono);
        var entry = Page(canvas.transform, "Entry", true, out var entryFrame);
        SidePanels(entryFrame, round);
        var lobby = Page(canvas.transform, "Lobby", false, out var lobbyFrame);
        SidePanels(lobbyFrame, round);
        var match = Page(canvas.transform, "Match", false, out var matchFrame);
        var boardArt = Sprite(ArtRoot + "New/UI/Board.png");
        MatchSurfaces(matchFrame, round, boardArt);
        var status = Text(canvas.transform, "Status", "", body, OnlineTheme.BodySize, OnlineTheme.Amber);
        Place(status.rectTransform, OnlineTheme.Content.x, 908, OnlineTheme.Content.width, 72);
        status.textWrappingMode = TextWrappingModes.Normal;

        var controller = new GameObject("BusaraOnline");
        var browser = controller.AddComponent<OnlineBrowserTransport>();
        var session = controller.AddComponent<OnlineSession>();
        var screen = controller.AddComponent<OnlineMvpScreen>();
        screen.font = body;
        screen.boardArt = boardArt;
        screen.resourceIcons = new[]
        {
            Sprite(ArtRoot + "WaterIcon.png"), Sprite(ArtRoot + "EarthIcon.png"),
            Sprite(ArtRoot + "FireIcon.png"), Sprite(ArtRoot + "AirIcon.png")
        };
        SerializedWiring.Set(screen, "session", session);
        SerializedWiring.Set(screen, "browser", browser);
        SerializedWiring.Set(screen, "entryPage", entry);
        SerializedWiring.Set(screen, "lobbyPage", lobby);
        SerializedWiring.Set(screen, "matchPage", match);
        SerializedWiring.Set(screen, "statusText", status);
        SerializedWiring.Set(screen, "connectionText", connection);
        SerializedWiring.Set(screen, "buttonPrefab", buttonPrefab);
        SerializedWiring.Set(screen, "labelPrefab", labelPrefab);
        SerializedWiring.Set(screen, "roundSprite", round);
        SerializedWiring.Set(screen, "circleSprite", circle);
    }

    private static TMP_Text Header(Transform canvas, Sprite round, TMP_FontAsset bold, TMP_FontAsset mono)
    {
        var title = Text(canvas, "Title", "BUSARA", bold, 40, OnlineTheme.Amber);
        Place(title.rectTransform, 36, 8, 600, 62);
        title.overflowMode = TextOverflowModes.Overflow;
        title.characterSpacing = 8;
        var subtitle = Text(canvas, "Subtitle", "ONLINE  \u00b7  PRIVATE TABLE", mono, 18, OnlineTheme.TextMuted);
        Place(subtitle.rectTransform, 40, 70, 600, 24);
        subtitle.characterSpacing = 4;
        var pill = Rounded(Node("Connection pill", canvas, 1164, 30, 400, 46).gameObject.AddComponent<Image>(), round, OnlineTheme.Surface);
        var connection = Text(pill.transform, "Connection", "", mono, 18, OnlineTheme.Text);
        Stretch(connection.rectTransform, 16, 4);
        connection.alignment = TextAlignmentOptions.Center;
        var rule = Node("Header rule", canvas, 36, 98, 1528, 2).gameObject.AddComponent<Image>();
        rule.color = OnlineTheme.Hex(0xFFB21A, .5f);
        rule.raycastTarget = false;
        return connection;
    }

    private static OnlinePage Page(Transform canvas, string id, bool start, out RectTransform frame)
    {
        var page = EgUIBuilder.CreateScreen<OnlinePage>(canvas, id);
        EgUIBuilder.Set(page, "showOnStart", start);
        var area = OnlineTheme.Content;
        frame = Node("Frame", EgUIBuilder.Content(page), area.x, area.y, area.width, area.height);
        var content = Node("Content", frame, 0, 0, area.width, area.height);
        SerializedWiring.Set(page, "content", content);
        return page;
    }

    private static void SidePanels(RectTransform frame, Sprite round)
    {
        Panel("Story panel", frame, 0, 0, OnlineTheme.SideWidth, OnlineTheme.PanelHeight, round, OnlineTheme.Surface);
        float x = OnlineTheme.SideWidth + OnlineTheme.PanelGap;
        Panel("Action panel", frame, x, 0, OnlineTheme.Content.width - x, OnlineTheme.PanelHeight, round, OnlineTheme.Surface);
        frame.Find("Content").SetAsLastSibling();
    }

    private static void MatchSurfaces(RectTransform frame, Sprite round, Sprite boardArt)
    {
        Panel("Turn bar", frame, 0, 0, OnlineTheme.Content.width, OnlineTheme.TurnBarHeight, round, OnlineTheme.Surface);
        for (int seat = 0; seat < 2; seat++)
        {
            Panel("Seat " + seat, frame, OnlineTheme.SeatX(seat), OnlineTheme.SeatY, OnlineTheme.SeatWidth,
                OnlineTheme.SeatHeight, round, OnlineTheme.Surface);
            var art = Node("Board art " + seat, frame, OnlineTheme.ArtX(seat), OnlineTheme.ArtY, OnlineTheme.ArtSize,
                OnlineTheme.ArtSize).gameObject.AddComponent<Image>();
            art.sprite = boardArt;
            art.raycastTarget = false;
        }
        frame.Find("Content").SetAsLastSibling();
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
