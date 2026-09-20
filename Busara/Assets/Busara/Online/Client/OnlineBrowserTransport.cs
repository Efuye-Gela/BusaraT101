using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Busara.Online.Client
{
    public sealed class OnlineBrowserTransport : MonoBehaviour
    {
        [Serializable] public sealed class Envelope
        {
            public string kind;
            public string requestId;
            public int status;
            public string body;
        }
        [Serializable] public sealed class Route
        {
            public string matchId;
            public bool hasInvite;
            public string backend;
            public float pollSeconds;
            public bool hidden;
        }
        [Serializable] public sealed class Visibility { public bool hidden; }

        private readonly Dictionary<string, Action<Envelope>> requests = new Dictionary<string, Action<Envelope>>();
        public event Action<Envelope> Event;
        public bool Available
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public void Initialize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Busara_Init(gameObject.name);
#else
            Event?.Invoke(new Envelope { kind = "unsupported", body = "Online transport requires the HTTPS-hosted Unity Web build. No offline simulation is running." });
#endif
        }

        public void Request(string method, string path, string body, string csrf, Action<Envelope> callback)
        {
            string id = Guid.NewGuid().ToString("N");
            requests.Add(id, callback);
#if UNITY_WEBGL && !UNITY_EDITOR
            Busara_Request(id, method, path, body ?? "", csrf ?? "");
#else
            OnBrowserEvent(JsonUtility.ToJson(new Envelope { kind = "response", requestId = id, status = 0,
                body = "Use the Unity Web build on the local HTTPS server." }));
#endif
        }

        public void Bind(string guestId, string matchId, string csrf)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Busara_Bind(guestId, matchId, csrf);
#endif
        }

        public bool StoreOutbox(string body)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Busara_StoreOutbox(body) == 1;
#else
            return false;
#endif
        }

        public bool ClearOutbox(string commandId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Busara_ClearOutbox(commandId) == 1;
#else
            return false;
#endif
        }

        public void RestoreRoomOutbox(string guestId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Busara_RestoreRoomOutbox(guestId);
#endif
        }

        public bool StoreRoomOutbox(string operation)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Busara_StoreRoomOutbox(operation) == 1;
#else
            return false;
#endif
        }

        public bool ClearRoomOutbox(string commandId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Busara_ClearRoomOutbox(commandId) == 1;
#else
            return false;
#endif
        }

        public void CopyInvite(string invite)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Busara_CopyInvite(invite);
#endif
        }

        public void PublishVisibleControls(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Debug.isDebugBuild) Busara_VisibleControls(json);
#endif
        }

        public void OnBrowserEvent(string json)
        {
            Envelope message;
            try { message = JsonUtility.FromJson<Envelope>(json); }
            catch (ArgumentException) { ProtocolFailure(); return; }
            if (message == null || string.IsNullOrEmpty(message.kind) ||
                (message.kind == "response" && string.IsNullOrEmpty(message.requestId)))
            {
                ProtocolFailure();
                return;
            }
            if ((message.kind == "response" || message.kind == "protocolError") && message.requestId != null &&
                requests.TryGetValue(message.requestId, out Action<Envelope> callback))
            {
                requests.Remove(message.requestId);
                callback(message);
            }
            else Event?.Invoke(message);
        }

        private void ProtocolFailure()
        {
            var callbacks = new List<Action<Envelope>>(requests.Values);
            requests.Clear();
            var failure = new Envelope { kind = "protocolError", body = "Malformed browser response. Saved requests are retained; retry safely." };
            foreach (var callback in callbacks) callback(failure);
            Event?.Invoke(failure);
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Busara_Dispose();
#endif
            requests.Clear();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void Busara_Init(string receiver);
        [DllImport("__Internal")] private static extern void Busara_Request(string id, string method, string path, string body, string csrf);
        [DllImport("__Internal")] private static extern void Busara_Bind(string guestId, string matchId, string csrf);
        [DllImport("__Internal")] private static extern int Busara_StoreOutbox(string body);
        [DllImport("__Internal")] private static extern int Busara_ClearOutbox(string id);
        [DllImport("__Internal")] private static extern void Busara_RestoreRoomOutbox(string guestId);
        [DllImport("__Internal")] private static extern int Busara_StoreRoomOutbox(string operation);
        [DllImport("__Internal")] private static extern int Busara_ClearRoomOutbox(string commandId);
        [DllImport("__Internal")] private static extern void Busara_CopyInvite(string invite);
        [DllImport("__Internal")] private static extern void Busara_VisibleControls(string json);
        [DllImport("__Internal")] private static extern void Busara_Dispose();
#endif
    }
}
