using System;
using System.Collections.Generic;
using System.Linq;
using Busara.Online;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BusaraOnlineParityTests
{
    [Test]
    public void OnlineCatalogMatchesAuthoredKingdomsAndRecipes()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<KingdomCatalog>(
            "Assets/Busara/ScriptableObjects/Kingdoms/KingdomCatalog.asset");
        Assert.That(catalog, Is.Not.Null);
        var supported = new[]
        {
            (Definitions.Egolica, "Egolica", typeof(Abundance), 0, PowerTiming.OwnTurn),
            (Definitions.Mask, "Mask of Light", typeof(Retraction), 2, PowerTiming.OtherTurn),
            (Definitions.Knowledge, "N'evulandis", typeof(InfiniteKnowledge), 1, PowerTiming.OwnTurn)
        };
        foreach (var entry in supported)
        {
            Kingdom kingdom = catalog.kingdoms.Single(item => item.kingdomName == entry.Item2);
            Assert.That(kingdom.power.GetType(), Is.EqualTo(entry.Item3));
            Assert.That(kingdom.power.virtueCost, Is.EqualTo(entry.Item4));
            Assert.That(kingdom.power.timing, Is.EqualTo(entry.Item5));
            Assert.That(kingdom.power.onlyWhileHidden, Is.EqualTo(entry.Item1 == Definitions.Egolica));
            CollectionAssert.AreEquivalent(kingdom.virtuesForWin.Select(goal =>
                new KeyValuePair<VirtueType, int>(goal.virtues.type, goal.NumberofVirtues)), Definitions.Goals(entry.Item1));
        }
        foreach (VirtueType type in Enum.GetValues(typeof(VirtueType)))
        {
            Virtue virtue = AssetDatabase.LoadAssetAtPath<Virtue>(
                "Assets/Busara/ScriptableObjects/Virtues/" + type + ".asset");
            Assert.That(virtue, Is.Not.Null);
            Assert.That(Definitions.Forge(virtue.componentOne, virtue.componentTwo), Is.EqualTo(virtue.type));
            Assert.That(Definitions.Forge(virtue.componentTwo, virtue.componentOne), Is.EqualTo(virtue.type));
        }
    }

    [Test]
    public void OnlineBoardsSetupAndResourceOnlyDeckMatchAuthoredScene()
    {
        Scene preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
        List<Slot> previous = BoardManager.slots;
        try
        {
            PlayerManager players = preview.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<PlayerManager>(true)).Single();
            DeckManager deck = preview.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<DeckManager>(true)).Single();
            MatchState domain = DomainRules.Create("catalog-parity");
            for (int seat = 0; seat < 2; seat++)
            {
                Player player = players.Players[seat];
                CollectionAssert.AreEqual(player.setUpCard.collectionResources, Definitions.Setup(seat));
                CollectionAssert.AreEquivalent(player.Board.Slots.Select(slot => slot.Index),
                    domain.board.Where(slot => slot.seat == seat).Select(slot => slot.id));
            }
            var resources = deck.Cards.OfType<ResourceCard>().ToArray();
            Assert.That(resources.Length, Is.EqualTo(24));
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
                Assert.That(resources.Count(card => card.Resource == type), Is.EqualTo(6));
            BoardManager.slots = players.Players.Take(2).SelectMany(player => player.Board.Slots).ToList();
            foreach (Slot slot in BoardManager.slots)
                CollectionAssert.AreEquivalent(BoardManager.GetAdjacentSlots(slot).Select(item => item.Index),
                    domain.board.Where(item => SharedRules.Adjacent(slot.Index, item.id)).Select(item => item.id));
        }
        finally
        {
            BoardManager.slots = previous;
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    [Test]
    public void SharedPaymentRetainsOfflineReferenceOwnershipAndMultiplicity()
    {
        Virtue owned = ScriptableObject.CreateInstance<Virtue>();
        Virtue sameTypeButNotOwned = ScriptableObject.CreateInstance<Virtue>();
        try
        {
            owned.type = sameTypeButNotOwned.type = VirtueType.Art;
            Assert.That(PowerRules.CanPay(new[] { owned, owned }, new[] { owned, owned }, 2), Is.True);
            Assert.That(PowerRules.CanPay(new[] { owned }, new[] { owned, owned }, 2), Is.False);
            Assert.That(PowerRules.CanPay(new[] { owned }, new[] { sameTypeButNotOwned }, 1), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owned);
            UnityEngine.Object.DestroyImmediate(sameTypeButNotOwned);
        }
    }

    [Test]
    public void UnityJsonPreservesDecisionContractFieldsAndStringVersion()
    {
        var view = new ClientView
        {
            matchId = "match", version = "9007199254740993", seat = 1,
            decision = new DecisionView { id = "decision", owner = 1, kind = "Retraction", paymentCost = 2 },
            choices = new List<LegalChoice> { new LegalChoice { kind = "use", paymentCost = 2 } }
        };
        ClientView restored = JsonUtility.FromJson<ClientView>(JsonUtility.ToJson(view));
        Assert.That(restored.version, Is.EqualTo(view.version));
        Assert.That(restored.decision.id, Is.EqualTo("decision"));
        Assert.That(restored.decision.owner, Is.EqualTo(1));
        Assert.That(restored.choices.Single().paymentCost, Is.EqualTo(2));
    }
}
