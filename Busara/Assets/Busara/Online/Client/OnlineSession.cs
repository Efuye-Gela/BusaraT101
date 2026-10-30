using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Busara.Online.Client
{
    public sealed class OnlineSession : MonoBehaviour
    {
        [Serializable] private sealed class RoomRequest { public string commandId; }
        [Serializable] private sealed class RoomOperation
        {
            public string path;
            public string body;
            public bool matchingInvite;
        }
        [Serializable] private sealed class ApiError { public string code; public string message; }
        public ClientView View { get; private set; }
        public GuestView Guest { get; private set; }
        public string Status { get; private set; } = "Connecting to the guest session…";
        public string Connection { get; private set; } = "Connecting";
        public bool Busy { get; private set; }
        public bool HasInvite { get; private set; }
        public bool NeedsGuest { get; private set; }
        public bool Pending => pending != null;
        public bool RoomPending => roomRequestBody != null;
        public bool RoomOperationsReady => roomOutboxReady;
        public bool CanRetryRoom => RoomPending && roomOutboxReady && !Busy &&
            (roomRequestPath != "/api/rooms/join" || HasInvite);
        public bool CanDiscardRoom { get; private set; }
        public bool CanAct => Guest != null && View != null && outboxReady && projectionHealthy && !Busy && !Pending && !expired;
        public bool CanResolveConflict { get; private set; }
        public string InviteUrl { get; private set; }
        public event Action Changed;
        private OnlineBrowserTransport transport;
        private string matchId;
        private string pendingBody;
        private OnlineCommand pending;
        private bool expired;
        private bool outboxReady;
        private bool projectionHealthy;
        private string confirmedVersion;
        private AdaptivePollSchedule polling = new AdaptivePollSchedule(4, 4, 4, false);
        private bool usesUgs;
        private string roomRequestBody;
        private string roomRequestPath;
        private string roomCommandId;
        private bool roomOutboxReady;
        private IOnlineDiagnostics diagnostics = new BrowserOnlineDiagnostics();
        private int pendingSample;
        private bool recoveringPending;

        public void Initialize(OnlineBrowserTransport browser)
        {
            transport = browser;
            transport.Event += OnEvent;
            transport.Initialize();
        }

        private void OnEvent(OnlineBrowserTransport.Envelope e)
        {
            switch (e.kind)
            {
                case "route":
                    var route = Parse<OnlineBrowserTransport.Route>(e.body);
                    if (route == null)
                    {
                        Status = "Protocol error: navigation data is unreadable. Reload to retry.";
                        break;
                    }
                    matchId = route.matchId;
                    HasInvite = route.hasInvite;
                    usesUgs = route.backend == "ugs";
                    float normalPoll = route.pollSeconds > 0 ? route.pollSeconds : 4;
                    polling = usesUgs ? new AdaptivePollSchedule(normalPoll, 2, Math.Max(30, normalPoll)) :
                        new AdaptivePollSchedule(normalPoll, normalPoll, normalPoll, false);
                    polling.SetBackground(usesUgs && route.hidden, Time.unscaledTime);
                    RefreshGuest();
                    break;
                case "visibility":
                    var visibility = Parse<OnlineBrowserTransport.Visibility>(e.body);
                    if (visibility == null) Status = "Protocol error: browser visibility data is unreadable.";
                    else if (usesUgs) polling.SetBackground(visibility.hidden, Time.unscaledTime);
                    break;
                case "outbox":
                    outboxReady = true;
                    if (!string.IsNullOrEmpty(e.body))
                    {
                        pending = Parse<OnlineCommand>(e.body);
                        if (pending != null && Guid.TryParse(pending.commandId, out _) && ValidVersion(pending.expectedVersion))
                        {
                            pendingBody = e.body;
                            recoveringPending = true;
                            RetryPending();
                        }
                        else
                        {
                            pending = null;
                            outboxReady = false;
                            Status = "The saved pending action is unreadable. Actions are blocked; do not clear browser storage to recover a seat.";
                        }
                    }
                    FetchView();
                    break;
                case "roomOutbox":
                    roomOutboxReady = true;
                    if (!string.IsNullOrEmpty(e.body))
                    {
                        var operation = Parse<RoomOperation>(e.body);
                        var request = operation == null ? null : Parse<RoomRequest>(operation.body);
                        if (request == null || !Guid.TryParse(request.commandId, out _) ||
                            (operation.path != "/api/rooms" && operation.path != "/api/rooms/join"))
                        {
                            roomOutboxReady = false;
                            Status = "Protocol error: saved room operation is unreadable. No replacement request was sent.";
                            break;
                        }
                        roomRequestPath = operation.path;
                        roomRequestBody = operation.body;
                        roomCommandId = request.commandId;
                        if (roomRequestPath == "/api/rooms/join") HasInvite = operation.matchingInvite;
                        if (CanRetryRoom) RetryRoom();
                        else Status = "Invitation acceptance is unresolved. Reopen the SAME private invitation to retry the saved request. No invitation secret is stored.";
                    }
                    break;
                case "roomOutboxLocked":
                    roomOutboxReady = false;
                    Status = "Another tab owns your pending room request. Close that tab and reload to resume.";
                    break;
                case "protocolError":
                    Busy = false;
                    projectionHealthy = false;
                    Status = "Protocol error: malformed browser response. Saved actions are retained. Retry safely.";
                    break;
                case "invalidate": FetchView(); break;
                case "connected": Connection = "Live"; FetchView(); break;
                case "polling": Connection = "UGS - HTTPS polling"; FetchView(); break;
                case "reconnecting": Connection = "Reconnecting — HTTPS polling active"; break;
                case "storageError": Status = "Browser storage is unavailable. Pending actions cannot be protected; enable site storage before playing."; break;
                case "outboxLocked":
                    outboxReady = false;
                    Status = "Another tab owns this room's pending action. Close that tab, then reload this one to resume safely.";
                    break;
                case "copied": Status = e.body; break;
                case "unsupported": Status = e.body; Connection = "Web build required"; break;
            }
            Changed?.Invoke();
        }

        public void RefreshGuest()
        {
            Busy = true;
            transport.Request("GET", "/api/guest", "", "", response => AcceptGuest(response, false));
            Changed?.Invoke();
        }

        public void CreateGuest()
        {
            if (Busy || Guest != null || !NeedsGuest) return;
            Busy = true;
            transport.Request("POST", "/api/guest", "{}", "", response => AcceptGuest(response, true));
            Changed?.Invoke();
        }

        private void AcceptGuest(OnlineBrowserTransport.Envelope response, bool creating)
        {
            Busy = false;
            if (response.status >= 200 && response.status < 300)
            {
                Guest = Parse<GuestView>(response.body);
                if (Guest == null || string.IsNullOrEmpty(Guest.guestId) || string.IsNullOrEmpty(Guest.csrfToken))
                {
                    Status = "The server returned an invalid guest session.";
                    Guest = null;
                }
                else
                {
                    expired = false;
                    NeedsGuest = false;
                    Status = "Guest session active. It expires after 30 days; refresh does not extend it.";
                    if (!string.IsNullOrEmpty(matchId)) transport.Bind(Guest.guestId, matchId, Guest.csrfToken);
                    else transport.RestoreRoomOutbox(Guest.guestId);
                }
            }
            else if (!creating && (response.status == 401 || response.status == 404 || response.status == 410))
            {
                // An existing room must never silently acquire a replacement identity.
                NeedsGuest = string.IsNullOrEmpty(matchId);
                Status = NeedsGuest
                    ? "No valid guest session. Create a new 30-day guest explicitly to create or accept an invitation. An old seat cannot be recovered this way."
                    : "Guest session expired or unavailable. This room cannot be resumed with a new identity or invitation.";
                expired = !NeedsGuest;
            }
            else Status = Failure(response);
            Changed?.Invoke();
        }

        public void CreateRoom() { BeginRoom("/api/rooms"); }
        public void JoinRoom() { if (HasInvite) BeginRoom("/api/rooms/join"); }
        public void RetryRoom() { if (CanRetryRoom) BeginRoom(roomRequestPath); }
        private void BeginRoom(string path)
        {
            if (Guest == null || Busy || Pending || View != null || !roomOutboxReady) return;
            if (roomRequestBody == null)
            {
                string commandId = Guid.NewGuid().ToString();
                string body = JsonUtility.ToJson(new RoomRequest { commandId = commandId });
                if (!transport.StoreRoomOutbox(JsonUtility.ToJson(new RoomOperation { path = path, body = body })))
                {
                    Status = "Room request NOT sent: durable storage unavailable or a different request is pending. Reload to reconcile.";
                    Changed?.Invoke();
                    return;
                }
                roomRequestPath = path; roomRequestBody = body; roomCommandId = commandId;
            }
            if (roomRequestPath != path)
            {
                Status = "Retry the unresolved room request before creating a different room.";
                Changed?.Invoke();
                return;
            }
            Busy = true;
            CanDiscardRoom = false;
            transport.Request("POST", path, roomRequestBody, Guest.csrfToken, response =>
            {
                Busy = false;
                if (response.status >= 200 && response.status < 300)
                {
                    var result = Parse<RoomResult>(response.body);
                    if (result == null || !Guid.TryParse(result.matchId, out _) || !ValidVersion(result.version))
                    {
                        Status = "Protocol error: malformed room receipt. The saved request is retained; retry identically.";
                        Changed?.Invoke();
                        return;
                    }
                    if (!transport.ClearRoomOutbox(roomCommandId))
                    {
                        Status = "Room receipt received, but saved request could not be cleared. Retry when storage is available.";
                        Changed?.Invoke();
                        return;
                    }
                    matchId = result.matchId;
                    InviteUrl = result.inviteUrl;
                    HasInvite = false;
                    roomRequestBody = null;
                    roomRequestPath = null;
                    roomCommandId = null;
                    transport.Bind(Guest.guestId, matchId, Guest.csrfToken);
                    Status = "Room connected. Set your name and readiness.";
                }
                else
                {
                    Status = Failure(response) + " Retry the same room action.";
                    CanDiscardRoom = response.status >= 400 && response.status < 500 &&
                        response.status != 401 && response.status != 429;
                }
                Changed?.Invoke();
            });
            Changed?.Invoke();
        }

        public void DiscardRejectedRoom()
        {
            if (!CanDiscardRoom || Busy || !transport.ClearRoomOutbox(roomCommandId)) return;
            roomRequestBody = null; roomRequestPath = null; roomCommandId = null;
            CanDiscardRoom = false;
            Status = "Rejected room request discarded explicitly. You may create a new request.";
            Changed?.Invoke();
        }

        public void CopyInvite() { if (!string.IsNullOrEmpty(InviteUrl)) transport.CopyInvite(InviteUrl); }

        public void Submit(LegalChoice choice, IList<string> paymentIds, string name = null, bool ready = false,
            IList<int> slots = null, IList<string> exchangeIds = null)
        {
            if (!CanAct || !outboxReady || choice == null || !View.choices.Contains(choice)) return;
            int sample = diagnostics.Begin("submit");
            var command = new OnlineCommand
            {
                commandId = Guid.NewGuid().ToString(),
                expectedVersion = View.version,
                decisionId = View.decision == null ? null : View.decision.id,
                kind = choice.kind, from = choice.from, to = choice.to, resourceType = choice.resourceType,
                name = name, ready = ready,
                paymentIds = paymentIds == null ? Array.Empty<string>() : new List<string>(paymentIds).ToArray(),
                slots = slots == null ? Array.Empty<int>() : new List<int>(slots).ToArray(),
                count = choice.count, virtueType = choice.virtueType,
                exchangeIds = exchangeIds == null ? Array.Empty<string>() : new List<string>(exchangeIds).ToArray()
            };
            string body = JsonUtility.ToJson(command);
            if (!transport.StoreOutbox(body))
            {
                diagnostics.Failed(sample, "not_dispatched");
                Status = "Action NOT sent: durable outbox unavailable or occupied. Enable storage, then refresh to reconcile.";
                Changed?.Invoke();
                return;
            }
            pending = command;
            pendingBody = body;
            pendingSample = sample;
            RetryPending();
        }

        public void RetryPending()
        {
            if (Guest == null || pending == null || Busy || expired) return;
            int sample = pendingSample != 0 ? pendingSample : diagnostics.Begin(recoveringPending ? "recovery" : "retry");
            pendingSample = 0;
            recoveringPending = false;
            Busy = true;
            Status = "Submitting saved action… No further actions are allowed until its receipt is known.";
            transport.Request("POST", RoomPath + (usesUgs ? "/commands-with-view" : "/commands"), pendingBody, Guest.csrfToken, response =>
            {
                Busy = false;
                // Only UGS's Cloud Code module embeds the actor's own updated view alongside
                // the receipt; the legacy backend still returns a bare CommandReceipt.
                CommandResult result = ParseCommandResult(response.body, usesUgs);
                CommandReceipt receipt = result.receipt;
                bool verified = receipt != null && receipt.commandId == pending.commandId && receipt.matchId == matchId &&
                    ValidVersion(receipt.version) &&
                    (receipt.status == "accepted" || receipt.status == "applied" || receipt.status == "rejected" ||
                     receipt.status == "conflict" || receipt.status == "ok");
                if (verified)
                {
                    diagnostics.Receipt(sample, receipt.version,
                        receipt.status == "accepted" || receipt.status == "applied" || receipt.status == "ok");
                    projectionHealthy = false;
                    if (confirmedVersion == null || CompareVersions(receipt.version, confirmedVersion) > 0)
                        confirmedVersion = receipt.version;
                    if (!transport.ClearOutbox(pending.commandId))
                    {
                        Status = "Receipt received, but saved action could not be cleared. Retry safely when storage is available.";
                    }
                    else
                    {
                        pending = null;
                        pendingBody = null;
                        projectionHealthy = false;
                        CanResolveConflict = false;
                        Status = receipt.status == "accepted" || receipt.status == "applied" || receipt.status == "ok"
                            ? "Action confirmed." : "Action rejected: " + receipt.code + ". Refresh and choose again.";
                    }
                    // UGS already returned our own updated projection alongside the receipt;
                    // apply it directly instead of spending a second round trip just to see
                    // the move we already know we made. Fall back to a real fetch if that
                    // embedded view is missing/stale, or unconditionally on the legacy backend
                    // (which has no embedded view to apply).
                    if (result?.view == null || ApplyFreshView(result.view) != ViewOutcome.Applied) FetchView();
                }
                else
                {
                    diagnostics.Failed(sample, response.kind == "protocolError" ||
                        (response.status >= 200 && response.status < 300) ? "unverified_receipt" :
                        response.status == 0 ? "delivery_uncertain" : "http_rejected");
                    CanResolveConflict = response.status == 409 || response.status == 422 || response.status == 400;
                    if (response.status == 401 || response.status == 410) expired = true;
                    Status = (response.status >= 200 && response.status < 300
                        ? "Protocol error: malformed or mismatched command receipt." : Failure(response)) +
                        " The identical pending action is retained. Retry; do not submit a replacement.";
                    if (CanResolveConflict) FetchView();
                }
                Changed?.Invoke();
            }, sample);
            Changed?.Invoke();
        }

        public void ResolveConflict()
        {
            if (!CanResolveConflict || Busy || pending == null || polling.InFlight) return;
            if (!transport.ClearOutbox(pending.commandId)) return;
            pending = null;
            pendingBody = null;
            CanResolveConflict = false;
            Status = "Rejected pending action discarded explicitly. Review the refreshed position and choose again.";
            FetchView();
            Changed?.Invoke();
        }

        public void FetchView()
        {
            if (Guest == null || string.IsNullOrEmpty(matchId) || polling.InFlight || expired) return;
            polling.Started(Time.unscaledTime);
            transport.Request("GET", RoomPath, "", "", response =>
            {
                bool validProjection = false;
                bool stale = false;
                if (response.status >= 200 && response.status < 300)
                {
                    ViewOutcome outcome = ApplyFreshView(Parse<ClientView>(response.body));
                    validProjection = outcome != ViewOutcome.Invalid;
                    stale = outcome == ViewOutcome.Stale;
                }
                else
                {
                    projectionHealthy = false;
                    if (response.status == 401 || response.status == 410) expired = true;
                    Connection = "Reconnecting — HTTPS polling active";
                    Status = Failure(response);
                }
                polling.Completed(Time.unscaledTime, validProjection, CurrentPollActivity);
                if (stale) polling.RetryStaleProjection(Time.unscaledTime);
                Changed?.Invoke();
            });
        }

        private enum ViewOutcome { Invalid, Stale, Applied }

        private ViewOutcome ApplyFreshView(ClientView fresh)
        {
            if (fresh == null || fresh.matchId != matchId ||
                !Definitions.IsKnown(fresh.ruleset) ||
                !ValidVersion(fresh.version))
            {
                projectionHealthy = false;
                Status = "Unsupported or malformed match projection. No actions will be inferred.";
                return ViewOutcome.Invalid;
            }
            if (confirmedVersion != null && CompareVersions(fresh.version, confirmedVersion) < 0) return ViewOutcome.Stale;
            if (usesUgs) Connection = "UGS - HTTPS polling";
            confirmedVersion = fresh.version;
            projectionHealthy = true;
            if (View == null || CompareVersions(fresh.version, View.version) > 0) View = fresh;
            diagnostics.ViewApplied(View.version);
            polling.SetActivity(CurrentPollActivity, Time.unscaledTime);
            return ViewOutcome.Applied;
        }

        private PollActivity CurrentPollActivity => View != null && View.phase == "Finished" ? PollActivity.Finished :
            View == null || View.phase == "Lobby" || View.awaitingOther ? PollActivity.Waiting : PollActivity.LocalTurn;
        private string RoomPath => "/api/rooms/" + Uri.EscapeDataString(matchId);
        public void NotifyViewRendered(string version)
        {
            if (View == null || version != View.version) return;
            int ticket = diagnostics.PrepareFrame(version);
            if (ticket != 0) StartCoroutine(ObserveFrameBoundary(ticket));
        }

        private IEnumerator ObserveFrameBoundary(int ticket)
        {
            yield return new WaitForEndOfFrame();
            diagnostics.FrameBoundary(ticket);
        }

        private void Update() { if (!Busy && polling.IsDue(Time.unscaledTime)) FetchView(); }
        private static bool ValidVersion(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value) if (c < '0' || c > '9') return false;
            return true;
        }
        public static int CompareVersions(string left, string right)
        {
            if (!ValidVersion(left) || !ValidVersion(right)) throw new ArgumentException("Versions must be decimal strings.");
            left = left.TrimStart('0'); right = right.TrimStart('0');
            return left.Length == right.Length ? string.CompareOrdinal(left, right) : left.Length.CompareTo(right.Length);
        }
        private static string Failure(OnlineBrowserTransport.Envelope response)
        {
            if (response.kind == "protocolError") return "Protocol error: malformed browser response.";
            if (response.status == 0) return "Backend unavailable or delivery uncertain.";
            if (response.status == 401 || response.status == 410) return "Guest session expired or unavailable; no replacement identity was created.";
            if (response.status == 403) return "Access denied. This guest does not own this seat or request authorization failed.";
            if (response.status == 409) return "The action conflicts with the current room revision.";
            if (response.status == 429) return "Too many requests. Wait, then retry.";
            ApiError error = Parse<ApiError>(response.body);
            if (error != null && error.code == "guest_unregistered")
                return "This UGS identity is not registered for Busara. Create the guest explicitly before joining.";
            if (error != null && error.code == "storage_capacity_reached")
                return "UGS storage capacity reached. The request was not saved; ask the project administrator. Pending actions are retained.";
            if (error != null && !string.IsNullOrEmpty(error.code))
                return "Server rejected the request (" + error.code + ", HTTP " + response.status + ").";
            return "Server request failed (HTTP " + response.status + ").";
        }
        private static T Parse<T>(string json) where T : class
        {
            try { return JsonUtility.FromJson<T>(json); }
            catch (ArgumentException) { return null; }
        }
        private static CommandResult ParseCommandResult(string json, bool withView)
        {
            var result = (withView ? Parse<CommandResult>(json) : null) ?? new CommandResult();
            if (string.IsNullOrEmpty(result.receipt?.commandId))
            {
                result.receipt = Parse<CommandReceipt>(json);
                result.view = null;
            }
            return result;
        }
        private void OnDestroy() { if (transport != null) transport.Event -= OnEvent; }
    }
}
