using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class PlayerSetupTests
{
    private GameObject root;
    private PlayerManager players;
    private TurnManager turns;
    private BoardManager boards;
    private List<Player> seats;
    private KingdomCatalog catalog;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Setup tests");
        players = root.AddComponent<PlayerManager>();
        PlayerManager.Instance = players;
        turns = root.AddComponent<TurnManager>();
        TurnManager.Instance = turns;
        boards = root.AddComponent<BoardManager>();
        BoardManager.Instance = boards;
        boards.gameBoards = new List<Board>();
        catalog = AssetDatabase.LoadAssetAtPath<KingdomCatalog>(
            "Assets/Busara/ScriptableObjects/Kingdoms/KingdomCatalog.asset");
        Assert.That(catalog, Is.Not.Null);
        players.kingdomCatalog = catalog;
        players.requirePlayerSetup = true;
        players.dealKingdomsFromCatalog = true;
        players.Players = new List<Player>();
        SetupCard setup = AssetDatabase.LoadAssetAtPath<SetupCard>(
            AssetDatabase.GUIDToAssetPath("80dd86cd9fc43cc4293898653984c23e"));
        for (int i = 0; i < 4; i++)
        {
            GameObject obj = new GameObject($"Seat {i}");
            obj.transform.SetParent(root.transform);
            Player player = obj.AddComponent<Player>();
            player.Name = $"Original {i}";
            player.Board = obj.AddComponent<Board>();
            player.Board.player = player;
            player.Board.Slots = new List<Slot>();
            player.setUpCard = setup;
            var slotObject = new GameObject("Slot");
            slotObject.transform.SetParent(obj.transform);
            Slot slot = slotObject.AddComponent<Slot>();
            slot.Index = i * 16;
            slot.board = player.Board;
            player.Board.Slots.Add(slot);
            players.Players.Add(player);
            boards.gameBoards.Add(player.Board);
        }
        seats = new List<Player>(players.Players);
        turns.firstPlayer = seats[0];
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        BoardManager.slots.Clear();
        PlayerManager.Instance = null;
        TurnManager.Instance = null;
        BoardManager.Instance = null;
        SelectionManager.Instance = null;
    }

    private List<PlayerSetupEntry> Entries(int count)
    {
        return Enumerable.Range(0, count)
            .Select(index => new PlayerSetupEntry($"Player {index + 1}", catalog.kingdoms[index])).ToList();
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    [TestCase(5, false)]
    [TestCase(6, false)]
    public void PlayerCountIsLimitedToRealSceneSeats(int count, bool valid)
    {
        Assert.That(players.TryConfigurePlayers(Entries(count), out _), Is.EqualTo(valid));
        Assert.That(turns.ActivePlayer, Is.Null);
        Assert.That(turns.TurnsStarted, Is.False);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("This player name is longer than thirty two characters")]
    public void InvalidNamesDoNotMutateAnySeats(string name)
    {
        var entries = Entries(2);
        entries[1].Name = name;
        Assert.That(players.TryConfigurePlayers(entries, out _), Is.False);
        Assert.That(players.Players, Has.Count.EqualTo(4));
        Assert.That(seats[0].Name, Is.EqualTo("Original 0"));
        Assert.That(players.IsAwaitingSetup, Is.True);
    }

    [Test]
    public void RejectsDuplicateNamesAndKingdomsAndMissingKingdom()
    {
        var entries = Entries(2);
        entries[1].Name = " player 1 ";
        Assert.That(players.TryConfigurePlayers(entries, out _), Is.False);
        entries[1].Name = "Second";
        entries[1].Kingdom = entries[0].Kingdom;
        Assert.That(players.TryConfigurePlayers(entries, out _), Is.False);
        entries[1].Kingdom = null;
        Assert.That(players.TryConfigurePlayers(entries, out _), Is.False);
    }

    [Test]
    public void RejectsKingdomOutsideCatalogAndIncompleteCatalog()
    {
        Kingdom foreign = ScriptableObject.CreateInstance<Kingdom>();
        KingdomCatalog incomplete = ScriptableObject.CreateInstance<KingdomCatalog>();
        try
        {
            var entries = Entries(2);
            entries[1].Kingdom = foreign;
            Assert.That(players.TryConfigurePlayers(entries, out _), Is.False);
            incomplete.kingdoms.Add(null);
            Assert.That(PlayerSetupRules.Validate(Entries(2), incomplete, 4, out _), Is.False);
            Assert.That(PlayerSetupRules.Validate(Entries(2), null, 4, out _), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(foreign);
            Object.DestroyImmediate(incomplete);
        }
    }

    [Test]
    public void SetupFiltersSeatsWithoutChangingSlotsOrResourceCards()
    {
        var entries = Entries(2);
        entries[0].Name = "  Alice  ";
        entries[0].Kingdom = catalog.kingdoms[14];
        var setupCards = seats.Select(player => player.setUpCard).ToArray();
        Assert.That(players.TryConfigurePlayers(entries, out string error), Is.True, error);
        Assert.That(seats[0].Name, Is.EqualTo("Alice"));
        Assert.That(seats[0].Kingdom, Is.SameAs(catalog.kingdoms[14]));
        Assert.That(players.Players, Is.EqualTo(seats.Take(2)));
        Assert.That(boards.gameBoards, Is.EqualTo(seats.Take(2).Select(player => player.Board)));
        CollectionAssert.AreEqual(new[] { 0, 16 }, BoardManager.slots.Select(slot => slot.Index));
        Assert.That(seats[2].gameObject.activeSelf, Is.False);
        Assert.That(seats[3].gameObject.activeSelf, Is.False);
        CollectionAssert.AreEqual(setupCards, seats.Select(player => player.setUpCard));
        Assert.That(players.Players.All(player => !player.hasFinishedSettingUp), Is.True);
        Assert.That(turns.firstPlayer, Is.SameAs(seats[0]));
        Assert.That(turns.ActivePlayer, Is.Null, "Configuration alone must not start the resource turns.");
    }

    [Test]
    public void StartIsGatedUntilValidSetupAndCannotRestartOrRedeal()
    {
        LogAssert.Expect(LogType.Warning, "Finish player setup before starting the game.");
        turns.StartTurns();
        Assert.That(turns.ActivePlayer, Is.Null);
        Assert.That(players.TryConfigurePlayers(Entries(2), out _), Is.True);
        turns.StartTurns();
        Assert.That(turns.ActivePlayer, Is.SameAs(seats[0]));
        Assert.That(turns.TurnsStarted, Is.True);
        Assert.That(players.TryConfigurePlayers(Entries(3), out _), Is.False);
        Kingdom chosen = seats[0].Kingdom;
        LogAssert.Expect(LogType.Warning, "Kingdoms are selected during player setup and cannot be redealt.");
        players.DealKingdoms();
        turns.StartTurns();
        Assert.That(seats[0].Kingdom, Is.SameAs(chosen));
    }

    [Test]
    public void MissingSetupCardReducesSupportedCapacity()
    {
        seats[3].setUpCard = null;
        Assert.That(players.SupportedPlayerCount, Is.EqualTo(3));
        Assert.That(players.TryConfigurePlayers(Entries(4), out _), Is.False);
        Assert.That(players.TryConfigurePlayers(Entries(3), out _), Is.True);
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void ResourceSaveLoadUsesConfiguredBoardCount(int count)
    {
        SelectionManager.Instance = root.AddComponent<SelectionManager>();
        Assert.That(players.TryConfigurePlayers(Entries(count), out _), Is.True);
        string key = "PlayerSetupTests-" + System.Guid.NewGuid().ToString("N");
        try
        {
            boards.SaveGameState(key);
            Assert.That(PlayerPrefs.GetString(key).Length, Is.EqualTo(count));
            boards.LoadGameState(key);
            Assert.That(boards.gameBoards.SelectMany(board => board.Slots).All(slot => !slot.isOccupied), Is.True);
        }
        finally
        {
            PlayerPrefs.DeleteKey(key);
        }
    }
}
