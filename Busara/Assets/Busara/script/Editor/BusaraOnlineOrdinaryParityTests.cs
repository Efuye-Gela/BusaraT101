using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Busara.Online;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BusaraOnlineOrdinaryParityTests
{
    private GameObject root;
    private List<Slot> previousSlots;
    private readonly List<Player> players = new List<Player>();
    private MatchState state;
    private sealed class FixedRandom : IGameRandom { public int Next(int maximum) => 0; }

    [SetUp]
    public void Setup()
    {
        root = new GameObject("Online ordinary parity");
        root.SetActive(false);
        previousSlots = BoardManager.slots;
        BoardManager.slots = new List<Slot>();
        state = DomainRules.Join(DomainRules.Create("ordinary-parity"), 1, "Guest");
        state.seats[0].kingdom = Definitions.Egolica;
        state.seats[1].kingdom = Definitions.Mask;
        for (int seat = 0; seat < 2; seat++)
        {
            Player player = Component<Player>("Seat " + seat);
            player.Board = Component<Board>("Board " + seat);
            player.Board.player = player;
            player.Board.Slots = new List<Slot>();
            players.Add(player);
        }
        foreach (SlotState item in state.board)
        {
            Slot slot = Component<Slot>("Slot " + item.id);
            slot.Index = item.id;
            slot.board = players[item.seat].Board;
            slot.board.Slots.Add(slot);
            BoardManager.slots.Add(slot);
        }
        state.deck = Enumerable.Range(0, 24).Select(index =>
            new CardState { id = "card-" + index, type = (ResourceType)(index % 4) }).ToList();
        state.deckRemaining = 24;
        state.phase = "Action";
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(root);
        BoardManager.slots = previousSlots;
        players.Clear();
    }

    [Test]
    public void OnlineChainMatchesOfflineRecipesAndCumulativeRecipients()
    {
        var forge = Component<ForgeManager>("Forge");
        forge.AllVirtues = Enum.GetValues(typeof(VirtueType)).Cast<VirtueType>().Select(type =>
            AssetDatabase.LoadAssetAtPath<Virtue>("Assets/Busara/ScriptableObjects/Virtues/" + type + ".asset")).ToList();
        Assert.That(forge.AllVirtues.All(virtue => virtue != null), Is.True);
        int[] ids = { 2, 3, 4, 12, 11, 10 };
        ResourceType[] types = { ResourceType.Fire, ResourceType.Water, ResourceType.Earth,
            ResourceType.Air, ResourceType.Fire, ResourceType.Earth };
        var resources = ids.Select((id, index) => Piece(id, types[index])).ToList();
        Piece(24, ResourceType.Air);
        Piece(28, ResourceType.Water);
        var recipients = new List<Player>();
        MethodInfo recipe = typeof(ForgeManager).GetMethod("CheckForgeCombination", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo award = typeof(ForgeManager).GetMethod("UpdateForgeStatus", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int index = 1; index < resources.Count; index++)
        {
            Assert.That(resources[index - 1].IsAdjacentTo(resources[index]), Is.True);
            var virtue = (Virtue)recipe.Invoke(forge, new object[] { resources[index - 1], resources[index] });
            award.Invoke(forge, new object[] { resources[index - 1], resources[index], recipients, virtue });
        }
        state.snapshot = DomainRules.Capture(state);
        state = DomainRules.Apply(state, 0, new OnlineCommand
        {
            kind = "forgeChain", commandId = Guid.NewGuid().ToString(), expectedVersion = state.version.ToString(), slots = ids
        }, new FixedRandom()).state;
        for (int seat = 0; seat < 2; seat++)
            CollectionAssert.AreEqual(players[seat].Virtues.Select(virtue => virtue.type),
                state.seats[seat].virtues.Select(token => token.type));
    }

    [TestCase(3, 12, 4)]
    [TestCase(3, 4, 12)]
    [TestCase(3, 4, 20)]
    [TestCase(3, 11, 19)]
    public void WeaponConnectivityMatchesOfflineRegardlessOfSelectionOrder(int first, int second, int third)
    {
        var weapon = Component<WeaponActionMove>("Weapon");
        var ids = new[] { first, second, third };
        var resources = ids.Select(id => Piece(id, ResourceType.Fire)).ToList();
        var method = typeof(WeaponActionMove).GetMethod("AreResourcesAdjacent", BindingFlags.Instance | BindingFlags.NonPublic);
        bool offline = (bool)method.Invoke(weapon, new object[] { resources });
        Assert.That(OrdinarySelection.Error(state.board, 0, ids, true) == null, Is.EqualTo(offline));
    }

    [Test]
    public void UnityJsonPreservesOrderedSlotsAndTradeOfferAcrossClones()
    {
        var command = new OnlineCommand { kind = "weapon", slots = new[] { 3, 12, 4 } };
        var restored = JsonUtility.FromJson<OnlineCommand>(JsonUtility.ToJson(command));
        CollectionAssert.AreEqual(command.slots, restored.slots);
        Piece(0, ResourceType.Water);
        Piece(4, ResourceType.Fire);
        state.snapshot = DomainRules.Capture(state);
        state = DomainRules.Apply(state, 0, new OnlineCommand
        {
            kind = "trade", commandId = Guid.NewGuid().ToString(), expectedVersion = state.version.ToString(),
            from = 0, resourceType = (int)ResourceType.Fire
        }, new FixedRandom()).state;
        state = DomainRules.Clone(state);
        DomainRules.ValidateState(state);
        var decision = JsonUtility.FromJson<PendingDecision>(JsonUtility.ToJson(state.pending));
        Assert.That(state.ruleset, Is.EqualTo(Definitions.CurrentRuleset));
        Assert.That(decision.id, Is.EqualTo(state.pending.id));
        Assert.That(decision.offerFrom, Is.Zero);
        Assert.That(decision.offerResourceType, Is.EqualTo((int)ResourceType.Fire));
        Assert.That(decision.kind, Is.EqualTo("TradeResponse"));
        Assert.That(decision.continuation, Is.EqualTo("TradeSelect"));
        Assert.That(Projection.ForSeat(state, 1).choices.Select(choice => choice.kind),
            Is.EquivalentTo(new[] { "tradeAccept", "tradeReject" }));
    }

    private Resource Piece(int id, ResourceType type)
    {
        Resource resource = Component<Resource>("Resource " + id);
        resource.resourceType = type;
        Slot.OccupySlot(BoardManager.slots.Single(slot => slot.Index == id), resource);
        state.board[id].pieceId = "piece-" + id;
        state.board[id].type = type;
        return resource;
    }

    private T Component<T>(string name) where T : Component
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform);
        return child.AddComponent<T>();
    }
}
