using Eg.UI;
using UnityEngine;

namespace Busara.Online.Client
{
    // An authored Eg page (Entry, Lobby or Match). The controller fills Content from the server view.
    public sealed class OnlinePage : UIScreen
    {
        [SerializeField] private RectTransform content;

        public RectTransform Content => content;

        public void Clear()
        {
            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
    }
}
