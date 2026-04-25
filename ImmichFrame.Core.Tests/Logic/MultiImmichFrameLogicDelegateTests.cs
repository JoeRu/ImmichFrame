using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic;

[TestFixture]
public class MultiImmichFrameLogicDelegateTests
{
    [Test]
    public async Task GetAssets_PreservesSelectionStrategyOrder()
    {
        var serverSettings = new Mock<IServerSettings>();
        var generalSettings = new Mock<IGeneralSettings>();
        var accountSelectionStrategy = new Mock<IAccountSelectionStrategy>();
        var logger = new Mock<ILogger<MultiImmichFrameLogicDelegate>>();
        var accountSettings = new Mock<IAccountSettings>();
        var accountLogic = new Mock<IAccountImmichFrameLogic>();

        serverSettings.SetupGet(x => x.GeneralSettings).Returns(generalSettings.Object);
        serverSettings.SetupGet(x => x.Accounts).Returns(new[] { accountSettings.Object });
        accountLogic.SetupGet(x => x.AccountSettings).Returns(accountSettings.Object);

        accountSelectionStrategy
            .Setup(x => x.GetAssets())
            .ReturnsAsync(new[]
            {
                (accountLogic.Object, CreateAsset("asset-1")),
                (accountLogic.Object, CreateAsset("asset-2")),
                (accountLogic.Object, CreateAsset("asset-3"))
            });

        var sut = new MultiImmichFrameLogicDelegate(
            serverSettings.Object,
            account => accountLogic.Object,
            logger.Object,
            accountSelectionStrategy.Object);

        var result = (await sut.GetAssets()).ToList();

        Assert.That(result.Select(x => x.Id), Is.EqualTo(new[] { "asset-1", "asset-2", "asset-3" }));
    }

    private static AssetResponseDto CreateAsset(string id) => new()
    {
        Id = id
    };
}
