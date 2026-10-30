using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Busara.Online.Client
{
    // The left panel, as in the offline game: the viewed player's kingdom and power, their virtue goal and forged
    // virtues, and the resource combinations that forge each virtue. Clicking a board views that player.
    public sealed partial class OnlineMvpScreen
    {
        [Serializable] public sealed class KingdomText
        {
            public string id;
            public string name;
            [TextArea] public string story;
            [TextArea] public string power;
        }

        private static readonly string[] InfoTabs = { "kingdom", "virtues", "combos" };
        private string infoTab = "kingdom";
        private int viewedSeat = -1;

        private void Info(ClientView view)
        {
            for (int seat = 0; seat < 2; seat++)
            {
                int target = seat;
                var viewButton = Button("view-seat-" + seat, "View seat " + (seat + 1), () =>
                {
                    viewedSeat = target;
                    if (infoTab == "combos") infoTab = "virtues";
                    Rebuild();
                });
                if (viewButton != null) viewButton.image.color = OnlineTheme.Hex(0xE8C27A, seat == ViewedSeat(view) ? .08f : 0f);
            }
            var player = view.players.Find(p => p.seat == ViewedSeat(view));
            if (player == null) return;
            foreach (string tab in InfoTabs)
            {
                string chosen = tab;
                Mark(Button("info-tab-" + tab, char.ToUpperInvariant(tab[0]) + tab.Substring(1),
                    () => { infoTab = chosen; Rebuild(); }), infoTab == tab);
            }
            Label("Info title", "Seat " + (player.seat + 1) + "  " + Dot + "  " + player.name +
                (player.seat == view.seat ? "  (you)" : ""));
            var kingdom = string.IsNullOrEmpty(player.kingdomId) ? null :
                KingdomCatalog.All.FirstOrDefault(k => k.Id == player.kingdomId);
            if (infoTab == "kingdom") KingdomInfo(player, kingdom);
            else if (infoTab == "virtues") VirtueInfo(player, kingdom);
            else Combos();
        }

        private int ViewedSeat(ClientView view) => view.players.Any(p => p.seat == viewedSeat) ? viewedSeat : view.seat;

        private void KingdomInfo(PlayerView player, KingdomDefinition kingdom)
        {
            if (kingdom == null)
            {
                bool known = !string.IsNullOrEmpty(player.kingdom);
                Label("Kingdom name", known ? player.kingdom : "Kingdom hidden");
                Label("Kingdom story", known ? "" : "This kingdom stays secret until it is revealed.");
                return;
            }
            var text = kingdomTexts.FirstOrDefault(k => k.id == kingdom.Id);
            Label("Kingdom name", kingdom.Name + (player.revealed ? "  " + Dot + "  revealed" : ""));
            Label("Kingdom story", text != null ? text.story : "");
            Label("Power name", kingdom.PowerName + "  " + Dot + "  " +
                (kingdom.Cost == 0 ? "free" : "costs " + kingdom.Cost + " virtue" + (kingdom.Cost == 1 ? "" : "s")));
            Label("Power description", text != null ? text.power : "");
        }

        private void VirtueInfo(PlayerView player, KingdomDefinition kingdom)
        {
            Label("Goal heading", "Virtue goal");
            if (kingdom == null) Label("Goal note", "The goal is hidden with the kingdom.");
            else
                for (int i = 0; i < kingdom.Goals.Length && i < 3; i++)
                {
                    var goal = kingdom.Goals[i];
                    int type = (int)goal.Key;
                    var row = Use<Image>("Goal row " + i);
                    if (row == null) continue;
                    Child<Image>(row, "Icon").sprite = virtueIcons[type];
                    Child<TMP_Text>(row, "Name").text = goal.Key.ToString();
                    Child<Image>(row, "First").sprite = resourceIcons[(int)Definitions.RecipeFirst[type]];
                    Child<Image>(row, "Second").sprite = resourceIcons[(int)Definitions.RecipeSecond[type]];
                    int have = player.virtuesVisible ? player.virtues.Count(t => t.type == goal.Key) : 0;
                    var count = Child<TMP_Text>(row, "Count");
                    count.text = player.virtuesVisible ? Math.Min(have, goal.Value) + " / " + goal.Value : "? / " + goal.Value;
                    count.color = have >= goal.Value ? OnlineTheme.Gold : OnlineTheme.Text;
                }
            Label("Forged heading", "Forged virtues");
            if (!player.virtuesVisible)
            {
                Label("Forged note", "Virtues hidden.");
                return;
            }
            for (int type = 0; type < 6; type++)
            {
                var cell = Use<Image>("Forged " + type);
                if (cell == null) continue;
                int count = player.virtues.Count(t => (int)t.type == type);
                Child<TMP_Text>(cell, "Count").text = "×" + count;
                Child<Image>(cell, "Icon").color = count > 0 ? Color.white : OnlineTheme.Hex(0xFFFFFF, .35f);
            }
        }

        // The six recipes are authored rows; Definitions decides which resources forge which virtue.
        private void Combos()
        {
            Label("Combos heading", "Resource combinations");
            for (int type = 0; type < 6; type++) Use<Image>("Combo " + type);
        }

        private static T Child<T>(Component parent, string name) where T : Component =>
            parent.transform.Find(name).GetComponent<T>();
    }
}
