using System;
using Busara.Online;
using Busara.Online.Client;
using NUnit.Framework;

public sealed class OnlineResourceSelectionTests
{
    [Test]
    public void SelectionPreservesOrderAndRemovingAnEarlierResourceTruncatesTheChain()
    {
        var selection = new OnlineResourceSelection();
        selection.Begin("forgeChain");
        foreach (int slot in new[] { 2, 3, 4, 12 }) selection.Toggle(slot);
        CollectionAssert.AreEqual(new[] { 2, 3, 4, 12 }, selection.Slots);
        selection.Toggle(3);
        CollectionAssert.AreEqual(new[] { 2 }, selection.Slots);
        selection.Toggle(10);
        CollectionAssert.AreEqual(new[] { 2, 10 }, selection.Slots);
    }

    [Test]
    public void SelectionCopiesItsPayloadAndNewActionOrRevisionCanClearIt()
    {
        var selection = new OnlineResourceSelection();
        selection.Begin("forgeChain");
        selection.Toggle(1);
        selection.Slots[0] = 30;
        Assert.AreEqual(1, selection.Slots[0]);
        selection.Begin("weapon");
        Assert.IsEmpty(selection.Slots);
        selection.Toggle(2);
        selection.Clear();
        Assert.IsFalse(selection.Active);
        Assert.IsEmpty(selection.Slots);
        Assert.Throws<ArgumentException>(() => selection.Begin("move"));
        Assert.Throws<InvalidOperationException>(() => selection.Toggle(2));
    }

    [Test]
    public void ConfirmationUsesTheSameSelectionRulesAsTheAuthority()
    {
        var view = new ClientView { seat = 0 };
        view.board = DomainRules.Create(Guid.NewGuid().ToString()).board;
        foreach (int slot in new[] { 2, 3, 4 })
        {
            view.board[slot].pieceId = "piece-" + slot;
            view.board[slot].type = slot == 3 ? ResourceType.Water : ResourceType.Fire;
        }
        var selection = new OnlineResourceSelection();
        selection.Begin("forgeChain");
        selection.Toggle(2);
        Assert.IsNotNull(selection.Error(view));
        selection.Toggle(3);
        selection.Toggle(4);
        Assert.IsNull(selection.Error(view));
        selection.Begin("weapon");
        foreach (int slot in new[] { 2, 3, 4 }) selection.Toggle(slot);
        Assert.IsNotNull(selection.Error(view));
        view.board[3].type = ResourceType.Fire;
        Assert.IsNull(selection.Error(view));
        view.seat = 1;
        Assert.IsNotNull(selection.Error(view));
    }

    [Test]
    public void JsonUtilityPreservesOrderedSlotsWithTheImmutableCommand()
    {
        var command = new OnlineCommand
        {
            commandId = Guid.NewGuid().ToString(), expectedVersion = "123",
            kind = "forgeChain", slots = new[] { 3, 4, 12, 11 }
        };
        var restored = UnityEngine.JsonUtility.FromJson<OnlineCommand>(UnityEngine.JsonUtility.ToJson(command));
        CollectionAssert.AreEqual(command.slots, restored.slots);
        Assert.AreEqual(command.commandId, restored.commandId);
    }
}
