using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Busara.Online.Client
{
    // Every control is authored in OnlineMVP under its page's Content and named by its id. Rendering shows the
    // controls a view needs and fills them in. Variable-length action lists copy the authored, inactive templates
    // inside the "Legal actions" list; nothing else is created at runtime.
    public sealed partial class OnlineMvpScreen
    {
        private const string TemplateSuffix = " Template";

        private static RectTransform Place(RectTransform rect, Transform parent, float x, float y, float width, float height)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private T Use<T>(string name) where T : Component
        {
            Transform child = content != null ? content.Find(name) : null;
            if (child == null)
            {
                Debug.LogError("OnlineMVP is missing the authored control '" + name + "' on the " + page.name + " page.");
                return null;
            }
            child.gameObject.SetActive(true);
            return child.GetComponent<T>();
        }

        private TMP_Text Label(string name, string value)
        {
            var label = Use<TMP_Text>(name);
            if (label != null) label.text = value;
            return label;
        }

        private Button Button(string id, string label, Action action, bool enabled = true)
        {
            var button = Use<Button>(id);
            if (button != null) Bind(button, id, label, action, enabled);
            return button;
        }

        private void Bind(Button button, string id, string label, Action action, bool enabled)
        {
            button.interactable = enabled;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;
            controls.Add(new Control { id = id, label = label, selectable = button });
        }

        private void Input(string id, string label, string value, Action<string> changed)
        {
            var input = Use<TMP_InputField>(id);
            if (input == null) return;
            input.onValueChanged.RemoveAllListeners();
            input.SetTextWithoutNotify(value);
            input.interactable = session.CanAct;
            input.onValueChanged.AddListener(v => changed(v));
            controls.Add(new Control { id = id, label = label, selectable = input });
        }

        // The authored action list, emptied of the previous render's rows.
        private RectTransform ActionList()
        {
            var scroll = Use<ScrollRect>("Legal actions");
            var list = scroll != null ? scroll.content : null;
            if (list == null) return null;
            for (int i = list.childCount - 1; i >= 0; i--)
            {
                var child = list.GetChild(i).gameObject;
                if (child.name.EndsWith(TemplateSuffix, StringComparison.Ordinal)) continue;
                child.SetActive(false);
                Destroy(child);
            }
            return list;
        }

        private T Copy<T>(RectTransform list, string template, string name) where T : Component
        {
            var source = list.Find(template + TemplateSuffix);
            var copy = Instantiate(source.gameObject, list, false);
            copy.name = name;
            copy.SetActive(true);
            return copy.GetComponent<T>();
        }

        private void ListLabel(RectTransform list, string label, ref float y, float height)
        {
            var text = Copy<TMP_Text>(list, "List Label", "Instructions");
            float width = list.rect.width - 24;
            Place(text.rectTransform, list, 12, y + 8, width, height - 16);
            text.text = label;
            // TMP's ellipsis blanks text whose lines overflow the rect, so the label grows to fit.
            float needed = text.GetPreferredValues(label, width, 0).y + 16;
            if (needed > height)
            {
                height = needed;
                text.rectTransform.sizeDelta = new Vector2(width, height - 16);
            }
            y += height;
        }

        private Button ListButton(RectTransform list, string id, string label, ref float y, Action action, bool enabled = true,
            bool primary = false)
        {
            var button = Copy<Button>(list, primary ? "List Primary" : "List Button", id);
            Place((RectTransform)button.transform, list, 10, y, list.rect.width - 20, 56);
            Bind(button, id, label, action, enabled);
            y += 66;
            return button;
        }
    }
}
