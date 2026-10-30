using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Busara.Online.Client
{
    public static class OnlineMenuEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "mainMenu" || !Application.CanStreamedLevelBeLoaded("OnlineMVP")) return;
            var root = new GameObject("Online MVP menu entry", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var rect = new GameObject("Online MVP (reduced variant)", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(root.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(32, 28);
            rect.sizeDelta = new Vector2(440, 70);
            rect.GetComponent<Image>().color = new Color(.07f, .095f, .13f);
            rect.GetComponent<Button>().onClick.AddListener(() => SceneManager.LoadScene("OnlineMVP"));
            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(rect, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12, 6);
            text.rectTransform.offsetMax = new Vector2(-12, -6);
            text.text = "Online MVP · private 2-player variant";
            text.fontSize = 22;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }
    }
}
