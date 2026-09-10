using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class HardWinterDisasterTests
{
    private GameObject root;
    private GameObject infoPanel;
    private GameObject otherPanel;
    private HardWinterDisaster disaster;
    private Player first;
    private Player empty;
    private Player last;
    private Virtue art;
    private Virtue nature;
    private readonly List<PlayerInfoCard> cards = new List<PlayerInfoCard>();
    private int completed;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Hard winter test");
        TurnManager.Instance = Component<TurnManager>("Turns");
        SelectionManager.Instance = Component<SelectionManager>("Selection");
        PlayerManager.Instance = Component<PlayerManager>("Players");
        PowerManager.Instance = Component<PowerManager>("Powers");
        ActionManager.Instance = Component<ActionManager>("Actions");
        DisasterManager.Instance = Component<DisasterManager>("Disasters");
        DisplayManager.Instance = Component<DisplayManager>("Display");
        DisplayManager.Instance.TopPanel = Child("Instructions").transform;
        DisplayManager.Instance.TopMassages = Component<TextMeshProUGUI>("Instructions text");
        DisplayManager.Instance.PopUpInfoPanel = Child("Popup");
        DisplayManager.Instance.PopUpText = Component<TextMeshProUGUI>("Popup text");
        DisplayManager.Instance.SideBarPanel = Child("Error").transform;
        DisplayManager.Instance.sideBarMassage = Component<TextMeshProUGUI>("Error text");
        art = ScriptableObject.CreateInstance<Virtue>();
        art.type = VirtueType.Art;
        nature = ScriptableObject.CreateInstance<Virtue>();
        nature.type = VirtueType.Nature;
        first = Player("First");
        empty = Player("Empty");
        last = Player("Last");
        PlayerManager.Instance.Players = new List<Player> { first, empty, last };
        SetField(TurnManager.Instance, "activePlayer", first);
        infoPanel = Child("Player info");
        otherPanel = Child("Other panel");
        DisplayManager.Instance.PlayerInfoPanel = infoPanel;
        DisplayManager.Instance.UIComponentList = new List<GameObject> { otherPanel };
        foreach (Player player in PlayerManager.Instance.Players)
            cards.Add(Card(player));
        infoPanel.SetActive(false);
        // EditMode fixtures do not run the normal Play Mode enable lifecycle.
        foreach (PlayerInfoCard card in cards)
        {
            InvokeLifecycle(card, "OnDisable");
            InvokeLifecycle(card, "OnEnable");
        }
        disaster = Component<HardWinterDisaster>("Hard winter");
        completed = 0;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (PlayerInfoCard card in cards)
            InvokeLifecycle(card, "OnDisable");
        InvokeLifecycle(disaster, "OnDisable");
        root.SetActive(false);
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(art);
        Object.DestroyImmediate(nature);
        cards.Clear();
        TurnManager.Instance = null;
        SelectionManager.Instance = null;
        PlayerManager.Instance = null;
        PowerManager.Instance = null;
        ActionManager.Instance = null;
        DisasterManager.Instance = null;
        DisplayManager.Instance = null;
    }

    [Test]
    public void RepeatedDisasterInstanceHasNewDiscardRevision()
    {
        first.Virtues.Add(art);
        disaster.Execute(disaster.GetAffectedPlayers(), () => completed++);
        int revision = disaster.DiscardRevision;
        Assert.That(disaster.TryDiscard(first, art), Is.True);
        first.Virtues.Add(art);
        disaster.Execute(disaster.GetAffectedPlayers(), () => completed++);
        Assert.That(disaster.DiscardRevision, Is.GreaterThan(revision));
        Assert.That(disaster.TryDiscard(first, art), Is.True);
        Assert.That(completed, Is.EqualTo(2));
    }

    [Test]
    public void OpensInfoAndWaitsForEachOwnedVirtueChoiceSkippingEmptyPlayers()
    {
        first.Virtues.AddRange(new[] { art, nature, nature });
        last.Virtues.Add(art);
        disaster.Execute(disaster.GetAffectedPlayers(), () => completed++);

        Assert.That(infoPanel.activeSelf, Is.True);
        Assert.That(otherPanel.activeSelf, Is.True, "Opening the popup should not switch sidebar tabs.");
        Assert.That(completed, Is.Zero);
        Assert.That(first.Virtues.Count, Is.EqualTo(3));
        Assert.That(ActionManager.Instance.CanPerformAction(), Is.False);
        AssertButton(first, art, true);
        AssertButton(first, nature, true);
        AssertButton(empty, art, false);
        AssertButton(last, art, false);

        Row(first, nature).DisasterDiscardButton.onClick.Invoke();
        CollectionAssert.AreEqual(new[] { art, nature }, first.Virtues);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(last));
        Assert.That(Row(first, nature).NumberOfvirtues.text, Is.EqualTo("1"));
        AssertButton(first, art, false);
        AssertButton(last, art, true);
        AssertButton(last, nature, false);
        Assert.That(completed, Is.Zero);

        Row(last, art).DisasterDiscardButton.onClick.Invoke();
        Assert.That(last.Virtues, Is.Empty);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(TurnManager.Instance.isSpecialCardDrawn, Is.False);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(first));
        Assert.That(HardWinterDisaster.Active, Is.Null);
        AssertButton(last, art, false);
        Assert.That(ActionManager.Instance.CanPerformAction(), Is.True);
    }

    [Test]
    public void DisasterManagerResumesAfterOriginalDrawerOnlyAfterLastDiscard()
    {
        first.Virtues.Add(art);
        last.Virtues.Add(nature);
        DisasterManager.Instance.TriggerDisaster(disaster);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(first));
        disaster.TryDiscard(first, art);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(last));
        disaster.TryDiscard(last, nature);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(empty));
        Assert.That(TurnManager.Instance.isSpecialCardDrawn, Is.False);
        Assert.That(HardWinterDisaster.Active, Is.Null);
    }

    [Test]
    public void WrongPlayerUnownedVirtueAndRepeatedClickCannotAdvanceOrSpend()
    {
        first.Virtues.Add(art);
        last.Virtues.Add(nature);
        disaster.Execute(disaster.GetAffectedPlayers(), () => completed++);
        Reject(last, nature);
        Reject(first, nature);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(first));
        Row(first, art).DisasterDiscardButton.onClick.Invoke();
        LogAssert.Expect(LogType.Warning,
            "Only the current affected player may discard one owned virtue; finish pending choices first.");
        Row(first, art).DisasterDiscardButton.onClick.Invoke();
        Assert.That(last.Virtues, Has.Count.EqualTo(1));
        Assert.That(completed, Is.Zero);
        Row(last, nature).DisasterDiscardButton.onClick.Invoke();
        LogAssert.Expect(LogType.Warning, "There is no virtue-loss disaster awaiting a discard.");
        Row(last, nature).DisasterDiscardButton.onClick.Invoke();
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void DirectTurnCompletionCannotBypassRequiredDiscard()
    {
        first.Virtues.Add(art);
        disaster.Execute(disaster.GetAffectedPlayers(), () => completed++);
        LogAssert.Expect(LogType.Warning, "Choose a virtue to discard before completing this special turn.");
        TurnManager.Instance.CompleteTurn(first);
        Assert.That(TurnManager.Instance.isSpecialCardDrawn, Is.True);
        Assert.That(first.Virtues, Has.Count.EqualTo(1));
        Assert.That(completed, Is.Zero);
    }

    [Test]
    public void PendingPowerChoiceBlocksDiscardWithoutSpending()
    {
        first.Virtues.Add(art);
        disaster.Execute(disaster.GetAffectedPlayers(), () => completed++);
        SetField(PowerManager.Instance, "<IsBusy>k__BackingField", true);
        Reject(first, art);
        Assert.That(first.Virtues, Has.Count.EqualTo(1));
        Assert.That(completed, Is.Zero);
        SetField(PowerManager.Instance, "<IsBusy>k__BackingField", false);
        Assert.That(disaster.TryDiscard(first, art), Is.True);
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void RemovesOwnedTokenByTypeWithoutChangingPowerPaymentSelection()
    {
        var equivalentDisplayAsset = ScriptableObject.CreateInstance<Virtue>();
        equivalentDisplayAsset.type = art.type;
        try
        {
            first.Virtues.AddRange(new[] { art, art });
            disaster.Execute(disaster.GetAffectedPlayers(), () => completed++);
            Assert.That(disaster.TryDiscard(first, equivalentDisplayAsset), Is.True);
            CollectionAssert.AreEqual(new[] { art }, first.Virtues);
            Assert.That(first.selectedVirtue, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(equivalentDisplayAsset);
        }
    }

    [Test]
    public void StaleParticipantListFiltersEmptyOutsidersAndDuplicates()
    {
        first.Virtues.Add(art);
        Player outsider = Player("Not participating");
        outsider.Virtues.Add(art);
        disaster.Execute(new List<Player> { empty, first, first, outsider }, () => completed++);
        Assert.That(disaster.TryDiscard(first, art), Is.True);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(outsider.Virtues, Has.Count.EqualTo(1));
    }

    [Test]
    public void NobodyWithVirtuesCompletesImmediatelyWithoutOpeningInfo()
    {
        disaster.Execute(disaster.GetAffectedPlayers(), () => completed++);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(infoPanel.activeSelf, Is.False);
        Assert.That(TurnManager.Instance.isSpecialCardDrawn, Is.False);
        Assert.That(HardWinterDisaster.Active, Is.Null);
    }

    [Test]
    public void HiddenVirtuesVisibleOnlyToCurrentDiscardOwnerNotTurnController()
    {
        first.virtuesHidden = last.virtuesHidden = true;
        first.Virtues.AddRange(new[] { art, art });
        last.Virtues.Add(nature);
        PowerManager.Instance.ControlTurn(last);
        disaster.Execute(disaster.GetAffectedPlayers(), () => completed++);
        Assert.That(Row(first, art).NumberOfvirtues.text, Is.EqualTo("2"));
        Assert.That(Row(last, nature).NumberOfvirtues.text, Is.EqualTo("?"));
        disaster.TryDiscard(first, art);
        Assert.That(Row(first, art).NumberOfvirtues.text, Is.EqualTo("?"));
        Assert.That(Row(last, nature).NumberOfvirtues.text, Is.EqualTo("1"));
        Assert.That(first.virtuesHidden && last.virtuesHidden, Is.True);
    }

    [Test]
    public void InfoCannotBeClosedUntilDiscardsFinish()
    {
        first.Virtues.Add(art);
        disaster.Execute(disaster.GetAffectedPlayers(), () => completed++);
        DisplayManager.Instance.ClosePlayerInfo();
        Assert.That(infoPanel.activeSelf, Is.True);
        disaster.TryDiscard(first, art);
        DisplayManager.Instance.ClosePlayerInfo();
        Assert.That(infoPanel.activeSelf, Is.False);
        Assert.That(otherPanel.activeSelf, Is.True);
    }

    [Test]
    public void CancelSpecialTurnClearsAllDiscardControls()
    {
        first.Virtues.Add(art);
        disaster.Execute(disaster.GetAffectedPlayers(), () => completed++);
        TurnManager.Instance.CancelSpecialTurn();
        Assert.That(HardWinterDisaster.Active, Is.Null);
        AssertButton(first, art, false);
        Assert.That(first.Virtues, Has.Count.EqualTo(1));
        Assert.That(completed, Is.Zero);
    }

    [Test]
    public void ResourceDeleteCannotSpendDuringVirtueDisaster()
    {
        first.Virtues.Add(art);
        disaster.Execute(disaster.GetAffectedPlayers(), () => completed++);
        Resource resource = Component<Resource>("Resource");
        LogAssert.Expect(LogType.Warning, "Choose a virtue, not a resource, for the hard winter disaster.");
        resource.OnclickDestroy();
        Assert.That(resource, Is.Not.Null);
        Assert.That(completed, Is.Zero);
    }

    private void Reject(Player player, Virtue virtue)
    {
        LogAssert.Expect(LogType.Warning,
            "Only the current affected player may discard one owned virtue; finish pending choices first.");
        Assert.That(disaster.TryDiscard(player, virtue), Is.False);
    }

    private void AssertButton(Player player, Virtue virtue, bool visible)
    {
        var button = Row(player, virtue).DisasterDiscardButton;
        Assert.That(button != null && button.gameObject.activeInHierarchy && button.interactable, Is.EqualTo(visible));
    }

    private VirtueUI Row(Player player, Virtue virtue)
    {
        return cards.Single(card => card.player == player).VirtueUIList.Single(row => row.virtueType == virtue);
    }

    private PlayerInfoCard Card(Player player)
    {
        var go = new GameObject(player.Name + " info", typeof(RectTransform));
        go.transform.SetParent(infoPanel.transform, false);
        go.SetActive(false);
        var card = go.AddComponent<PlayerInfoCard>();
        card.player = player;
        card.VirtueUIList = new List<VirtueUI>();
        foreach (Virtue virtue in new[] { art, nature })
        {
            var row = new GameObject(virtue.type.ToString(), typeof(RectTransform), typeof(VirtueUI))
                .GetComponent<VirtueUI>();
            row.transform.SetParent(go.transform, false);
            row.virtueType = virtue;
            row.currentPlayer = player;
            row.NumberOfvirtues = Component<TextMeshProUGUI>("Count");
            row.NumberOfvirtues.transform.SetParent(row.transform, false);
            card.VirtueUIList.Add(row);
        }
        go.SetActive(true);
        return card;
    }

    private Player Player(string name)
    {
        var player = Component<Player>(name);
        player.Name = name;
        player.Board = Component<Board>(name + " board");
        player.Board.Slots = new List<Slot>();
        return player;
    }

    private GameObject Child(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform);
        return go;
    }

    private T Component<T>(string name) where T : Component
    {
        return Child(name).AddComponent<T>();
    }

    private static void SetField(object owner, string name, object value)
    {
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    }

    private static void InvokeLifecycle(object owner, string name)
    {
        owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    }
}
