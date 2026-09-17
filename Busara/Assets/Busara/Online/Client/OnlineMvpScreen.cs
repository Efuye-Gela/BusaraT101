using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Busara.Online.Client
{
    // Online presentation inherits the authored dark TMP UI, resource icons and board art.
    // Two human seats share the original eight-column grid. Only server choices submit actions.
    public sealed class OnlineMvpScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        public Sprite boardArt;
        public Sprite[] resourceIcons = new Sprite[4];
        private static readonly Color Background = new Color(.035f, .05f, .075f);
        private static readonly Color Panel = new Color(.07f, .095f, .13f);
        private static readonly Color ButtonColor = new Color(.13f, .23f, .31f);
        private static readonly Color Accent = new Color(.91f, .72f, .36f);
        private OnlineSession session;
        private OnlineBrowserTransport browser;
        private RectTransform canvasRoot;
        private RectTransform content;
        private TMP_Text statusText;
        private TMP_Text connectionText;
        private readonly List<Control> controls = new List<Control>();
        private readonly HashSet<string> payment = new HashSet<string>();
        private string previousVersion;
        private string renderKey;
        private string draftName = "";
        private string mode;
        private int resourceFilter = -1;
        private int source = -1;
        private LegalChoice paying;
        private float nextDescriptors;

        [Serializable] private sealed class Control
        {
            public string id;
            public string label;
            public float x, y, width, height;
            public bool enabled;
            [NonSerialized] public Selectable selectable;
        }
        [Serializable] private sealed class VisibleUi
        {
            public string surface = "Unity OnlineMVP";
            public string version;
            public string phase;
            public string status;
            public string connection;
            public List<Control> controls;
        }

        private void Start()
        {
            gameObject.name = "BusaraOnline";
            browser = gameObject.AddComponent<OnlineBrowserTransport>();
            session = gameObject.AddComponent<OnlineSession>();
            var cameraObject = new GameObject("Online presentation camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.cullingMask = 0;
            canvasRoot = Rect("Online MVP Canvas", transform, 0, 0, 1600, 1000);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000);
            scaler.matchWidthOrHeight = .5f;
            canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
            canvasRoot.gameObject.AddComponent<Image>().color = Background;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Online EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform);
            }
            Label("Title", canvasRoot, "Busara  /  Online MVP", 34, 36, 20, 1100, 54);
            connectionText = Label("Connection", canvasRoot, "", 20, 1120, 30, 440, 50);
            statusText = Label("Status", canvasRoot, "", 21, 36, 900, 1528, 80);
            statusText.color = Accent;
            content = Rect("Online content", canvasRoot, 36, 90, 1528, 800);
            session.Changed += Refresh;
            Refresh();
            session.Initialize(browser);
        }

        private void Refresh()
        {
            statusText.text = session.Status;
            connectionText.text = session.Connection;
            var view = session.View;
            string key = (view == null ? "entry" : view.version) + "|" + session.Busy + "|" + session.Pending +
                "|" + session.NeedsGuest + "|" + (session.Guest != null) + "|" + session.HasInvite +
                "|" + session.CanResolveConflict + "|" + session.CanAct + "|" + session.RoomPending +
                "|" + session.RoomOperationsReady + "|" + session.CanDiscardRoom;
            if (key == renderKey) return;
            renderKey = key;
            if (view != null && previousVersion != view.version)
            {
                previousVersion = view.version;
                source = -1; mode = null; resourceFilter = -1; paying = null; payment.Clear();
                var self = view.players.Find(p => p.seat == view.seat);
                if (self != null && string.IsNullOrWhiteSpace(draftName)) draftName = self.name;
            }
            Rebuild();
        }

        private void Rebuild()
        {
            foreach (Transform child in content)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            controls.Clear();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (session.View == null) Entry();
            else if (session.View.phase == "Lobby") Lobby();
            else Board();
            if (session.Pending)
            {
                Button("retry-pending", "Retry identical pending action", content, 0, 744, 380, 50, session.RetryPending, !session.Busy);
                if (session.CanResolveConflict)
                    Button("resolve-conflict", "Discard rejected action and reselect", content, 396, 744, 440, 50,
                        session.ResolveConflict, !session.Busy);
            }
        }

        private void Scope(Transform parent, float x, float y, float width)
        {
            Label("Scope", parent,
                "Private • 2 human players • busara-online-mvp-v1\n\n" +
                "Random, distinct kingdoms: Egolica / Abundance (hidden-only, 0 virtues, up to 2 resources); " +
                "Mask of Light / Retraction (off-turn, 2 exact virtues); N'evulandis / Infinite Knowledge (own turn, 1 virtue).\n\n" +
                "Five native setup resources each; nonadjacent setup. A 24-card resource-only deck. Draw and place, " +
                "adjacent moves, exactly two-resource forging across boards, and native kingdom victory.\n\n" +
                "Not available: trades, weapons, disasters, the other 12 powers, longer forge chains, bots, matchmaking or spectators.\n\n" +
                "Disconnects wait indefinitely: no automatic Pass, forfeit or bot. Guests expire after a fixed 30 days; " +
                "there is no account or seat recovery. Invitations claim only the empty seat, expire after 24 hours, and stop working on use or start.",
                20, x, y, width, 570);
        }

        private void Entry()
        {
            Label("Entry heading", content, "A private table, with a server keeping the rules.", 30, 0, 0, 1460, 58);
            Scope(content, 0, 78, 920);
            Label("Entry actions", content, "Online MVP", 28, 990, 82, 520, 52);
            Label("Offline distinction", content,
                "Full Offline Play is unchanged in the desktop game's main menu. This separate Web entry never starts offline managers.",
                22, 990, 152, 510, 136);
            if (session.Guest == null)
            {
                Button("create-guest", "Create a new 30-day guest", content, 990, 320, 510, 60, session.CreateGuest,
                    session.NeedsGuest && !session.Busy);
                Button("retry-session", "Retry existing guest session", content, 990, 402, 510, 60, session.RefreshGuest, !session.Busy);
            }
            else
            {
                if (session.RoomPending)
                {
                    Button("retry-room", "Retry saved room request", content, 990, 320, 510, 60,
                        session.RetryRoom, session.CanRetryRoom);
                    if (session.CanDiscardRoom)
                        Button("discard-room", "Discard rejected room request", content, 990, 402, 510, 60,
                            session.DiscardRejectedRoom, !session.Busy);
                }
                else
                {
                    Button("create-room", "Create private room", content, 990, 320, 510, 60, session.CreateRoom,
                        !session.Busy && session.RoomOperationsReady);
                    Button("join-room", session.HasInvite ? "Accept private invitation" : "Open an invitation link to join",
                        content, 990, 402, 510, 60, session.JoinRoom,
                        session.HasInvite && !session.Busy && session.RoomOperationsReady);
                }
            }
        }

        private void Lobby()
        {
            var view = session.View;
            Label("Lobby heading", content, "Private room  /  You are seat " + (view.seat + 1), 30, 0, 0, 1400, 60);
            Scope(content, 0, 78, 870);
            int y = 76;
            foreach (var player in view.players)
            {
                Label("Seat " + player.seat, content,
                    "Seat " + (player.seat + 1) + "  ·  " + (player.joined ? player.name : "Waiting for invited player") +
                    "\n" + (player.ready ? "Ready" : "Not ready"), 25, 930, y, 580, 86);
                y += 108;
            }
            Input("player-name", "Your name", content, draftName, 930, 305, 570, 62, value => draftName = value);
            var self = view.players.Find(p => p.seat == view.seat);
            var configure = view.choices.Find(c => c.kind == "configure");
            if (configure != null)
                Button("configure", self != null && self.ready ? "Save name and mark not ready" : "Save name and mark ready",
                    content, 930, 387, 570, 64,
                    () => session.Submit(configure, null, draftName, self == null || !self.ready),
                    session.CanAct);
            var start = view.choices.Find(c => c.kind == "start");
            Button("start", "Start with random kingdoms", content, 930, 473, 570, 64,
                () => session.Submit(start, null), session.CanAct && start != null);
            if (!string.IsNullOrEmpty(session.InviteUrl))
                Button("copy-invite", "Copy single-use invitation", content, 930, 559, 570, 60, session.CopyInvite);
            Label("Room revision", content, "Revision " + view.version + "  ·  Host starts after both players are ready.",
                21, 0, 662, 1500, 64);
        }

        private void Board()
        {
            var view = session.View;
            string turn = view.draw ? "Draw — match finished" : view.winner >= 0 ? "Seat " + (view.winner + 1) + " wins" :
                view.awaitingOther ? "Waiting for the other player" :
                view.decision != null ? "Your decision" : view.activeSeat == view.seat ? "Your turn" : "Other player's turn";
            Label("Turn", content, turn + "  ·  " + view.phase + "  ·  revision " + view.version, 28, 0, 0, 1500, 56);
            for (int seat = 0; seat < 2; seat++)
            {
                var player = view.players.Find(p => p.seat == seat);
                if (player == null) continue;
                float x = seat * 496;
                Label("Board owner " + seat, content, "Seat " + (seat + 1) + "  " + player.name +
                    (seat == view.seat ? "  (you)" : ""), 24, x, 64, 480, 42);
                if (boardArt != null)
                {
                    var art = Rect("Authored board art " + seat, content, x, 118, 480, 472).gameObject.AddComponent<Image>();
                    art.sprite = boardArt;
                    art.color = new Color(1, 1, 1, .65f);
                    art.raycastTarget = false;
                }
                string kingdom = string.IsNullOrEmpty(player.kingdom) ? "Kingdom hidden" : player.kingdom;
                string virtues = !player.virtuesVisible ? "Virtues hidden" :
                    player.virtues.Count == 0 ? "No virtues" : string.Join(", ", player.virtues.Select(t => t.type.ToString()));
                Label("Player details " + seat, content, kingdom + (player.revealed ? " · revealed" : "") +
                    "\n" + virtues + (string.IsNullOrEmpty(player.goal) ? "" : "\nGoal: " + player.goal),
                    20, x, 604, 478, 120);
            }
            foreach (var slot in view.board)
            {
                int localColumn = slot.id % 8;
                float x = (localColumn / 4) * 496 + 60 + (localColumn % 4) * 96;
                float y = 166 + slot.id / 8 * 95;
                bool occupied = !string.IsNullOrEmpty(slot.pieceId);
                string text = slot.id + "\n" + (occupied ? slot.type.ToString() : "Empty");
                var button = Button("slot-" + slot.id, text, content, x, y, 84, 84,
                    () => ClickSlot(slot.id), session.CanAct && paying == null);
                button.image.color = source == slot.id ? new Color(.4f, .31f, .12f) : ButtonColor;
                if (occupied && (int)slot.type >= 0 && (int)slot.type < resourceIcons.Length && resourceIcons[(int)slot.type] != null)
                {
                    var icon = Rect("Authored resource", button.transform, 27, 5, 30, 30).gameObject.AddComponent<Image>();
                    icon.sprite = resourceIcons[(int)slot.type];
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    var label = button.GetComponentInChildren<TMP_Text>();
                    label.alignment = TextAlignmentOptions.Bottom;
                    label.fontSize = 16;
                }
            }
            var actions = Scroll("Legal actions", content, 1010, 64, 500, 658);
            if (paying != null) PaymentPanel(actions);
            else Choices(actions);
        }

        private void ClickSlot(int slot)
        {
            if (!session.CanAct) return;
            var candidates = FilterChoices().ToList();
            var destinations = candidates.Where(c => c.to == slot && (c.from < 0 || c.from == source)).ToList();
            if (destinations.Count == 1)
            {
                Choose(destinations[0]);
                return;
            }
            source = source == slot ? -1 : slot;
            Rebuild();
        }

        private IEnumerable<LegalChoice> FilterChoices()
        {
            return session.View.choices.Where(c => (mode == null || c.kind == mode) &&
                (resourceFilter < 0 || c.resourceType == resourceFilter) &&
                (source < 0 || c.from == source || (c.from < 0 && c.to == source)));
        }

        private void Choices(RectTransform actions)
        {
            var view = session.View;
            float y = 0;
            string prompt = view.decision != null ? view.decision.prompt :
                view.awaitingOther ? "The other player's required action is pending. Nothing will be passed automatically." :
                "Choose an action below. Board clicks filter a source or choose a unique legal destination.";
            ListLabel(actions, prompt, ref y, 120);
            if (mode != null || source >= 0)
                ListButton(actions, "clear-selection", "Show all legal actions", ref y, () =>
                { mode = null; source = -1; resourceFilter = -1; Rebuild(); });
            if (mode == null)
            {
                foreach (var group in view.choices.Where(c => c.to >= 0 || c.from >= 0)
                    .GroupBy(c => c.kind + ":" + (c.kind == "setupPlace" || c.kind == "abundancePlace" ? c.resourceType : -1)))
                {
                    var first = group.First();
                    int type = first.kind == "setupPlace" || first.kind == "abundancePlace" ? first.resourceType : -1;
                    string label = ActionName(first.kind) + (type < 0 ? "" : " " + ((ResourceType)type));
                    ListButton(actions, "mode-" + group.Key, label + " — select on board", ref y, () =>
                    { mode = first.kind; resourceFilter = type; source = -1; Rebuild(); });
                }
            }
            foreach (var choice in FilterChoices())
            {
                var selected = choice;
                string label = string.IsNullOrEmpty(choice.label) ? choice.kind : choice.label;
                if (choice.from >= 0) label += " · " + choice.from;
                if (choice.to >= 0) label += " → " + choice.to;
                if (choice.paymentCost > 0) label += " · pay " + choice.paymentCost;
                string id = "choice-" + choice.kind + "-" + choice.from + "-" + choice.to + "-" + choice.resourceType;
                ListButton(actions, id, label, ref y, () => Choose(selected), session.CanAct);
            }
            if (view.choices.Count == 0) ListLabel(actions, "No legal actions for your seat right now.", ref y, 80);
            actions.sizeDelta = new Vector2(0, y);
        }

        private void Choose(LegalChoice choice)
        {
            if (!session.CanAct) return;
            if (choice.paymentCost > 0)
            {
                paying = choice;
                payment.Clear();
                Rebuild();
            }
            else session.Submit(choice, null);
        }

        private static string ActionName(string kind)
        {
            switch (kind)
            {
                case "setupPlace": return "Set up";
                case "setupMove": return "Rearrange setup";
                case "move": return "Move resource";
                case "forge": return "Forge two resources";
                case "place": return "Place drawn resource";
                case "abundancePlace": return "Abundance: place";
                default: return kind;
            }
        }

        private void PaymentPanel(RectTransform actions)
        {
            var view = session.View;
            float y = 0;
            ListLabel(actions, (view.decision == null ? paying.label : view.decision.prompt) +
                "\nSelect exactly " + paying.paymentCost + " owned virtues. Each choice below is one distinct token.", ref y, 130);
            var self = view.players.Find(p => p.seat == view.seat);
            var options = view.decision != null && view.decision.owner == view.seat
                ? view.decision.paymentOptions : self.virtues;
            for (int i = 0; i < options.Count; i++)
            {
                var token = options[i];
                string label = (payment.Contains(token.id) ? "Selected · " : "Select · ") + token.type + " #" + (i + 1);
                ListButton(actions, "payment-" + i, label, ref y, () =>
                {
                    if (!payment.Remove(token.id) && payment.Count < paying.paymentCost) payment.Add(token.id);
                    Rebuild();
                }, session.CanAct);
            }
            ListButton(actions, "confirm-payment", "Confirm " + paying.kind + " · pay " + payment.Count + "/" + paying.paymentCost,
                ref y, () => session.Submit(paying, payment.ToList()), session.CanAct && payment.Count == paying.paymentCost);
            ListButton(actions, "cancel-payment", "Back — do not submit", ref y, () => { paying = null; payment.Clear(); Rebuild(); }, session.CanAct);
            var pass = view.choices.Find(c => c.kind == "pass");
            if (pass != null)
                ListButton(actions, "pass", "Pass without payment", ref y, () => session.Submit(pass, null), session.CanAct);
            actions.sizeDelta = new Vector2(0, y);
        }

        private RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private TMP_Text Label(string name, Transform parent, string value, int size, float x, float y, float width, float height)
        {
            var label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = value;
            label.fontSize = size;
            label.color = new Color(.94f, .95f, .97f);
            label.richText = false;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private Button Button(string id, string label, Transform parent, float x, float y, float width, float height,
            Action action, bool enabled = true)
        {
            var rect = Rect(id, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = enabled;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            colors.selectedColor = new Color(1.45f, 1.35f, 1.15f);
            colors.disabledColor = new Color(.55f, .55f, .55f);
            button.colors = colors;
            button.onClick.AddListener(() => action());
            var text = Label("Label", rect, label, 21, 10, 6, width - 20, height - 12);
            text.alignment = TextAlignmentOptions.Center;
            controls.Add(new Control { id = id, label = label, selectable = button });
            return button;
        }

        private void Input(string id, string label, Transform parent, string value, float x, float y, float width, float height, Action<string> changed)
        {
            var rect = Rect(id, parent, x, y, width, height);
            rect.gameObject.AddComponent<Image>().color = Panel;
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect("Viewport", rect, 12, 8, width - 24, height - 16);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label("Text", viewport, "", 23, 0, 0, width - 24, height - 16);
            var placeholder = Label("Placeholder", viewport, label, 23, 0, 0, width - 24, height - 16);
            placeholder.color = new Color(.7f, .76f, .82f);
            input.textViewport = viewport;
            input.textComponent = (TextMeshProUGUI)text;
            input.placeholder = placeholder;
            input.characterLimit = 40;
            input.text = value;
            input.interactable = session.CanAct;
            input.onValueChanged.AddListener(v => changed(v));
            controls.Add(new Control { id = id, label = label, selectable = input });
        }

        private RectTransform Scroll(string name, Transform parent, float x, float y, float width, float height)
        {
            var root = Rect(name, parent, x, y, width, height);
            root.gameObject.AddComponent<Image>().color = Panel;
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root, 8, 8, width - 16, height - 16);
            viewport.gameObject.AddComponent<RectMask2D>();
            var list = Rect("Choices", viewport, 0, 0, width - 16, height);
            list.anchorMax = new Vector2(1, 1);
            list.sizeDelta = new Vector2(0, height);
            scroll.viewport = viewport;
            scroll.content = list;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45;
            return list;
        }

        private void ListLabel(RectTransform list, string label, ref float y, float height)
        {
            Label("Instructions", list, label, 21, 12, y + 8, 450, height - 16);
            y += height;
        }
        private void ListButton(RectTransform list, string id, string label, ref float y, Action action, bool enabled = true)
        {
            Button(id, label, list, 10, y, 460, 62, action, enabled);
            y += 72;
        }

        private void LateUpdate()
        {
            if (!Debug.isDebugBuild || browser == null || Time.unscaledTime < nextDescriptors) return;
            nextDescriptors = Time.unscaledTime + .25f;
            var visible = new List<Control>();
            var corners = new Vector3[4];
            foreach (var control in controls)
            {
                if (control.selectable == null || !control.selectable.gameObject.activeInHierarchy) continue;
                var rect = (RectTransform)control.selectable.transform;
                rect.GetWorldCorners(corners);
                Vector2 bottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                Vector2 top = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                Vector2 center = (bottom + top) * .5f;
                if (center.x < 0 || center.y < 0 || center.x > Screen.width || center.y > Screen.height) continue;
                var clip = rect.GetComponentInParent<RectMask2D>();
                if (clip != null && !RectTransformUtility.RectangleContainsScreenPoint(clip.rectTransform, center, null)) continue;
                control.x = bottom.x / Screen.width;
                control.y = 1 - top.y / Screen.height;
                control.width = (top.x - bottom.x) / Screen.width;
                control.height = (top.y - bottom.y) / Screen.height;
                control.enabled = control.selectable.IsInteractable();
                visible.Add(control);
            }
            browser.PublishVisibleControls(JsonUtility.ToJson(new VisibleUi
            {
                version = session.View == null ? null : session.View.version,
                phase = session.View == null ? "Entry" : session.View.phase,
                status = session.Status, connection = session.Connection, controls = visible
            }));
        }
    }
}
