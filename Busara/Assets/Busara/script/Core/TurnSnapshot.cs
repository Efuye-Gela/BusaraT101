using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public sealed class SnapshotObservation
{
    public IReadOnlyDictionary<int, ResourceType> Board { get; }
    public IReadOnlyList<VirtueType> Virtues { get; }
    public Kingdom Kingdom { get; }

    internal SnapshotObservation(IEnumerable<KeyValuePair<int, ResourceType>> board,
        IEnumerable<VirtueType> virtues, Kingdom kingdom)
    {
        Board = new ReadOnlyDictionary<int, ResourceType>(board.ToDictionary(item => item.Key, item => item.Value));
        Virtues = virtues == null ? null : virtues.ToList().AsReadOnly();
        Kingdom = kingdom;
    }
}

public sealed class TurnSnapshot
{
    private sealed class PlayerSnapshot
    {
        public Player Player;
        public Kingdom Kingdom;
        public bool Hidden;
        public bool Revealed;
        public List<Virtue> Virtues;
        public string Board;
        public Dictionary<int, ResourceType> Resources;
        public HashSet<Player> KingdomViewers;
        public HashSet<Player> VirtueViewers;
    }

    private readonly List<PlayerSnapshot> players = new List<PlayerSnapshot>();
    private List<Card> cards;
    private int cardCount;

    public static TurnSnapshot Capture()
    {
        var snapshot = new TurnSnapshot();
        foreach (Player player in PlayerManager.Instance.Players)
        {
            snapshot.players.Add(new PlayerSnapshot
            {
                Player = player,
                Kingdom = player.Kingdom,
                Hidden = player.virtuesHidden,
                Revealed = player.kingdomRevealed,
                Virtues = new List<Virtue>(player.Virtues),
                Board = player.Board.GetBoardState(player.Board),
                Resources = PowerRules.Resources(player).ToDictionary(resource => resource.slot.Index,
                    resource => resource.resourceType),
                KingdomViewers = new HashSet<Player>(PlayerManager.Instance.Players.Where(player.CanSeeKingdom)),
                VirtueViewers = new HashSet<Player>(PlayerManager.Instance.Players.Where(player.CanSeeVirtues))
            });
        }
        if (DeckManager.Instance != null)
        {
            snapshot.cards = new List<Card>(DeckManager.Instance.Cards);
            snapshot.cardCount = DeckManager.Instance.CardCount;
        }
        return snapshot;
    }

    public SnapshotObservation Observe(Player subject, Player viewer)
    {
        PlayerSnapshot state = players.FirstOrDefault(item => item.Player == subject);
        if (state == null || viewer == null)
            return null;
        return new SnapshotObservation(state.Resources,
            state.VirtueViewers.Contains(viewer) && subject.CanSeeVirtues(viewer)
                ? state.Virtues.Select(virtue => virtue.type) : null,
            state.KingdomViewers.Contains(viewer) && subject.Kingdom == state.Kingdom &&
                subject.CanSeeKingdom(viewer) ? state.Kingdom : null);
    }

    public bool CanPay(Player player, List<Virtue> payment)
    {
        PlayerSnapshot state = players.FirstOrDefault(item => item.Player == player);
        return state != null && PowerRules.CanPay(state.Virtues, payment, payment.Count);
    }

    public void Restore()
    {
        SelectionManager.Instance.OnTurnEnd();
        var revealedKingdoms = new HashSet<Kingdom>(players.Select(state => state.Player)
            .Where(player => player.kingdomRevealed).Select(player => player.Kingdom));
        foreach (PlayerSnapshot state in players)
        {
            Player player = state.Player;
            player.Kingdom = state.Kingdom;
            player.kingdomRevealed = state.Revealed || revealedKingdoms.Contains(state.Kingdom);
            player.virtuesHidden = state.Hidden;
            player.Virtues.Clear();
            player.Virtues.AddRange(state.Virtues);
            player.hasDrawnResource = false;
            player.Board.LoadBoardState(player.Board, state.Board);
        }
        if (cards != null)
        {
            DeckManager.Instance.Cards.Clear();
            DeckManager.Instance.Cards.AddRange(cards);
            DeckManager.Instance.CardCount = cardCount;
        }
    }
}
