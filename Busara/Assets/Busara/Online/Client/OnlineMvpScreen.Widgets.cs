using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Busara.Online.Client
{
    // Variable-count controls are instantiated from the authored button/label prefabs, so every page shares one style.
    public sealed partial class OnlineMvpScreen
    {
        private static RectTransform Place(RectTransform rect, Transform parent, float x, float y, float width, float height)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private RectTransform Rect(string name, Transform parent, float x, float y, float width, float height) =>
            Place(new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(), parent, x, y, width, height);

        private Image Surface(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = roundSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 40f / OnlineTheme.Radius;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text Label(string name, Transform parent, string value, int size, float x, float y, float width, float height)
        {
            var label = Instantiate(labelPrefab);
            label.name = name;
            Place(label.rectTransform, parent, x, y, width, height);
            label.text = value;
            label.fontSize = size;
            return label;
        }

        private Button Button(string id, string label, Transform parent, float x, float y, float width, float height,
            Action action, bool enabled = true)
        {
            var button = Instantiate(buttonPrefab);
            button.name = id;
            Place((RectTransform)button.transform, parent, x, y, width, height);
            button.interactable = enabled;
            button.onClick.AddListener(() => action());
            var text = button.GetComponentInChildren<TMP_Text>(true);
            text.text = label;
            controls.Add(new Control { id = id, label = label, selectable = button });
            return button;
        }

        // The primary action on a surface: amber plate with ink text, mirroring the art's amber edge.
        private static Button Primary(Button button)
        {
            button.image.color = OnlineTheme.Amber;
            button.GetComponentInChildren<TMP_Text>(true).color = OnlineTheme.Ink;
            return button;
        }

        private void Input(string id, string label, Transform parent, string value, float x, float y, float width, float height, Action<string> changed)
        {
            var frame = Surface(id, parent, x, y, width, height, OnlineTheme.Well);
            frame.raycastTarget = true;
            var input = frame.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect("Viewport", frame.transform, 16, 8, width - 32, height - 16);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label("Text", viewport, "", 22, 0, 0, width - 32, height - 16);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var placeholder = Label("Placeholder", viewport, label, 22, 0, 0, width - 32, height - 16);
            placeholder.color = OnlineTheme.TextMuted;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = 40;
            input.text = value;
            input.interactable = session.CanAct;
            input.onValueChanged.AddListener(v => changed(v));
            controls.Add(new Control { id = id, label = label, selectable = input });
        }

        private RectTransform Scroll(string name, Transform parent, float x, float y, float width, float height)
        {
            var root = Surface(name, parent, x, y, width, height, OnlineTheme.Surface);
            root.raycastTarget = true;
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root.transform, 10, 10, width - 20, height - 20);
            viewport.gameObject.AddComponent<RectMask2D>();
            var list = Rect("Choices", viewport, 0, 0, width - 20, height);
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
            var text = Label("Instructions", list, label, OnlineTheme.BodySize, 12, y + 8, 456, height - 16);
            text.color = OnlineTheme.TextMuted;
            // TMP's ellipsis blanks text whose lines overflow the rect, so the label grows to fit.
            float needed = text.GetPreferredValues(label, 456, 0).y + 16;
            if (needed > height)
            {
                height = needed;
                text.rectTransform.sizeDelta = new Vector2(456, height - 16);
            }
            y += height;
        }

        private Button ListButton(RectTransform list, string id, string label, ref float y, Action action, bool enabled = true)
        {
            var button = Button(id, label, list, 10, y, 460, 56, action, enabled);
            y += 66;
            return button;
        }
    }
}
