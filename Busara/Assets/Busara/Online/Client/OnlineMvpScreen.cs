using System;
using System.Collections.Generic;
using Eg;
using Eg.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Busara.Online.Client
{
    // Controller for the authored OnlineMVP scene (built by BusaraOnlineSceneBuilder). It picks the Eg page that
    // matches the server phase and fills that page from the projected view. Only server choices submit actions.
    public sealed partial class OnlineMvpScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        public Sprite boardArt;
        public Sprite[] resourceIcons = new Sprite[4];
        [SerializeField] private OnlineSession session;
        [SerializeField] private OnlineBrowserTransport browser;
        [SerializeField] private OnlinePage entryPage;
        [SerializeField] private OnlinePage lobbyPage;
        [SerializeField] private OnlinePage matchPage;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text connectionText;
        [SerializeField] private Button buttonPrefab;
        [SerializeField] private TMP_Text labelPrefab;
        [SerializeField] private Sprite roundSprite;
        [SerializeField] private Sprite circleSprite;
        private IUIService ui;
        private OnlinePage page;
        private RectTransform content;
        private readonly List<Control> controls = new List<Control>();
        private readonly HashSet<string> payment = new HashSet<string>();
        private string previousVersion;
        private string renderKey;
        private string draftName = "";
        private string mode;
        private int resourceFilter = -1;
        private int source = -1;
        private LegalChoice paying;
        private readonly OnlineResourceSelection resourceSelection = new OnlineResourceSelection();
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

        private bool Wired => session != null && browser != null && entryPage != null && lobbyPage != null &&
            matchPage != null && statusText != null && connectionText != null && buttonPrefab != null && labelPrefab != null;

        private async void Start()
        {
            if (!Wired)
            {
                Debug.LogError("OnlineMVP scene is not wired. Rebuild it with Busara/Online/Build Online Scene.");
                enabled = false;
                return;
            }
            session.Changed += Refresh;
            Refresh();
            session.Initialize(browser);
            try
            {
                await EgBoot.WhenReady(destroyCancellationToken);
                ui = Services.Get<IUIService>();
            }
            catch (OperationCanceledException) { }
        }

        private void OnDestroy()
        {
            if (session != null) session.Changed -= Refresh;
        }

        // Page changes are reconciled every frame so a late start-page adoption or transition never strands the view.
        private void Update()
        {
            if (ui == null || page == null || ui.IsTransitioning || ui.CurrentPage == page) return;
            ShowPage(page);
        }

        private async void ShowPage(OnlinePage target)
        {
            try { await ui.ShowPageAsync(target.Id, null, false, destroyCancellationToken); }
            catch (OperationCanceledException) { }
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
                source = -1; mode = null; resourceFilter = -1; paying = null; payment.Clear(); exchange.Clear();
                resourceSelection.Clear();
                var self = view.players.Find(p => p.seat == view.seat);
                if (self != null && string.IsNullOrWhiteSpace(draftName)) draftName = self.name;
            }
            Rebuild();
        }

        private void Rebuild()
        {
            var view = session.View;
            var target = view == null ? entryPage : view.phase == "Lobby" ? lobbyPage : matchPage;
            if (page != null && page != target) page.Clear();
            target.Clear();
            page = target;
            content = target.Content;
            controls.Clear();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (view == null) Entry();
            else if (view.phase == "Lobby") Lobby();
            else Board();
            if (session.Pending)
            {
                Button("retry-pending", "Retry identical pending action", content, 0, 744, 380, 46, session.RetryPending, !session.Busy);
                if (session.CanResolveConflict)
                    Button("resolve-conflict", "Discard rejected action and reselect", content, 396, 744, 440, 46,
                        session.ResolveConflict, !session.Busy);
            }
            if (view != null) session.NotifyViewRendered(view.version);
        }
    }
}
