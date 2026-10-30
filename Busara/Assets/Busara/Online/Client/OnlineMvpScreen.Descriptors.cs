using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Busara.Online.Client
{
    public sealed partial class OnlineMvpScreen
    {
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
