using Eg.UI;
using UnityEngine;

namespace Busara.Online.Client
{
    // An authored Eg page (Entry, Lobby or Match). Its Content holds every control the page can show; the controller
    // shows and fills the ones the server view needs.
    public sealed class OnlinePage : UIScreen
    {
        [SerializeField] private RectTransform content;

        public RectTransform Content => content;

        // Hides every authored control; rendering shows the ones the current view uses.
        public void Clear()
        {
            if (content == null) return;
            for (int i = 0; i < content.childCount; i++)
                content.GetChild(i).gameObject.SetActive(false);
        }
    }
}
