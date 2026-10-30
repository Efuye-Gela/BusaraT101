using System.Reflection;
using Busara.Online;
using Busara.Online.Client;
using NUnit.Framework;
using UnityEngine;

public sealed class CommandReplyTests
{
    private static CommandResult Read(string json, bool withView = true) =>
        (CommandResult)typeof(OnlineSession).GetMethod("ParseCommandResult",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { json, withView });

    [TestCase(true)]
    [TestCase(false)]
    public void BareReceiptSurvivesReplyFormatRollout(bool withView)
    {
        var receipt = new CommandReceipt { commandId = "original", matchId = "room",
            version = "9007199254740993", status = "accepted" };
        var result = Read(JsonUtility.ToJson(receipt), withView);
        Assert.AreEqual(receipt.commandId, result.receipt.commandId);
        Assert.AreEqual(receipt.version, result.receipt.version);
        Assert.IsNull(result.view);
    }

    [Test]
    public void WrappedReplyKeepsReceiptAndActorView()
    {
        var source = new CommandResult {
            receipt = new CommandReceipt { commandId = "original", matchId = "room",
                version = "3", status = "accepted" },
            view = new ClientView { matchId = "room", version = "5", seat = 1,
                ruleset = "busara-online-mvp-v1", awaitingOther = true }
        };
        var result = Read(JsonUtility.ToJson(source));
        Assert.AreEqual("original", result.receipt.commandId);
        Assert.AreEqual("3", result.receipt.version);
        Assert.AreEqual("5", result.view.version);
        Assert.AreEqual(1, result.view.seat);
    }

    [TestCase("{invalid")]
    [TestCase("{}")]
    [TestCase("{\"code\":\"storage_busy\"}")]
    public void MalformedRepliesNeverInventAnAcknowledgement(string json)
    {
        var result = Read(json);
        Assert.IsTrue(string.IsNullOrEmpty(result.receipt?.commandId));
    }
}
