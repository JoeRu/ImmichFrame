using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.AccountSelection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic.AccountSelection;

[TestFixture]
public class TotalAccountImagesSelectionStrategyTests
{
    [Test]
    public async Task GetAssets_WithSingleAccount_PreservesAssetOrder()
    {
        var logger = new Mock<ILogger<TotalAccountImagesSelectionStrategy>>();
        var tracker = new Mock<IAssetAccountTracker>();
        var account = new Mock<IAccountImmichFrameLogic>();
        var accountSettings = new Mock<IAccountSettings>();

        account.SetupGet(x => x.AccountSettings).Returns(accountSettings.Object);
        account.Setup(x => x.GetTotalAssets()).ReturnsAsync(4);
        account.Setup(x => x.GetAssets()).ReturnsAsync(new[]
        {
            CreateAsset("asset-1"),
            CreateAsset("asset-2"),
            CreateAsset("asset-3"),
            CreateAsset("asset-4")
        });

        tracker.Setup(x => x.RecordAssetLocation(account.Object, It.IsAny<string>()))
            .Returns(ValueTask.FromResult(true));

        var strategy = new TotalAccountImagesSelectionStrategy(logger.Object, tracker.Object);
        strategy.Initialize(new[] { account.Object });

        var result = (await strategy.GetAssets()).ToList();

        Assert.That(result.Select(x => x.Item2.Id), Is.EqualTo(new[] { "asset-1", "asset-2", "asset-3", "asset-4" }));
    }

    private static AssetResponseDto CreateAsset(string id) => new()
    {
        Id = id
    };
}
