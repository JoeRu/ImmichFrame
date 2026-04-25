using ImmichFrame.Core.Api;
using ImmichFrame.Core.Exceptions;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic;
using ImmichFrame.Core.Logic.AccountSelection;
using ImmichFrame.Core.Models;
using Microsoft.Extensions.DependencyInjection;
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
        var tracker = new Mock<IAssetAccountTracker>();
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
            accountSelectionStrategy.Object,
            tracker.Object);

        var result = (await sut.GetAssets()).ToList();

        Assert.That(result.Select(x => x.Id), Is.EqualTo(new[] { "asset-1", "asset-2", "asset-3" }));
    }

    [Test]
    public async Task GetAsset_WhenTrackerMisses_FallsBackToMatchingAccount()
    {
        var assetId = Guid.NewGuid();
        const string rangeHeader = "bytes=0-1023";

        var accountSettingsA = new Mock<IAccountSettings>();
        accountSettingsA.SetupGet(x => x.ImmichServerUrl).Returns("https://a.example");

        var accountSettingsB = new Mock<IAccountSettings>();
        accountSettingsB.SetupGet(x => x.ImmichServerUrl).Returns("https://b.example");

        var accountA = new Mock<IAccountImmichFrameLogic>();
        accountA.SetupGet(x => x.AccountSettings).Returns(accountSettingsA.Object);
        accountA.Setup(x => x.GetAssetInfoById(assetId)).ThrowsAsync(new AssetNotFoundException());

        var expected = new AssetResponse
        {
            FileName = "asset.jpg",
            ContentType = "image/jpeg",
            FileStream = new MemoryStream([1, 2, 3]),
            IsPartial = false
        };

        var accountB = new Mock<IAccountImmichFrameLogic>();
        accountB.SetupGet(x => x.AccountSettings).Returns(accountSettingsB.Object);
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(new AssetResponseDto { Id = assetId.ToString() });
        accountB.Setup(x => x.GetAsset(assetId, AssetTypeEnum.IMAGE, rangeHeader)).ReturnsAsync(expected);

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.Accounts).Returns([accountSettingsA.Object, accountSettingsB.Object]);

        var selectionStrategy = new Mock<IAccountSelectionStrategy>();
        selectionStrategy
            .Setup(x => x.ForAsset(assetId, It.IsAny<Func<IAccountImmichFrameLogic, Task<AssetResponse>>>()))
            .Throws(new AssetNotFoundException());

        var tracker = new Mock<IAssetAccountTracker>();

        var logger = new Mock<ILogger<MultiImmichFrameLogicDelegate>>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(tracker.Object)
            .BuildServiceProvider();

        var sut = ActivatorUtilities.CreateInstance<MultiImmichFrameLogicDelegate>(
            serviceProvider,
            serverSettings.Object,
            (Func<IAccountSettings, IAccountImmichFrameLogic>)(settings =>
                ReferenceEquals(settings, accountSettingsA.Object) ? accountA.Object : accountB.Object),
            logger.Object,
            selectionStrategy.Object);

        var result = await sut.GetAsset(assetId, AssetTypeEnum.IMAGE, rangeHeader);

        Assert.That(result, Is.SameAs(expected));
        tracker.Verify(x => x.RecordAssetLocation(accountB.Object, assetId.ToString()), Times.Once);
        accountA.Verify(x => x.GetAssetInfoById(assetId), Times.Once);
        accountB.Verify(x => x.GetAssetInfoById(assetId), Times.Once);
        accountB.Verify(x => x.GetAsset(assetId, AssetTypeEnum.IMAGE, rangeHeader), Times.Once);
    }

    [Test]
    public async Task GetAsset_WhenRecacheFails_StillReturnsAsset()
    {
        var assetId = Guid.NewGuid();

        var accountSettings = new Mock<IAccountSettings>();
        accountSettings.SetupGet(x => x.ImmichServerUrl).Returns("https://fallback.example");

        var account = new Mock<IAccountImmichFrameLogic>();
        account.SetupGet(x => x.AccountSettings).Returns(accountSettings.Object);
        account.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(new AssetResponseDto { Id = assetId.ToString() });

        var expected = new AssetResponse
        {
            FileName = "asset.jpg",
            ContentType = "image/jpeg",
            FileStream = new MemoryStream([1, 2, 3]),
            IsPartial = false
        };
        account.Setup(x => x.GetAsset(assetId, null, null)).ReturnsAsync(expected);

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.Accounts).Returns([accountSettings.Object]);

        var selectionStrategy = new Mock<IAccountSelectionStrategy>();
        selectionStrategy
            .Setup(x => x.ForAsset(assetId, It.IsAny<Func<IAccountImmichFrameLogic, Task<AssetResponse>>>()))
            .Throws(new AssetNotFoundException());

        var tracker = new Mock<IAssetAccountTracker>();
        tracker
            .Setup(x => x.RecordAssetLocation(account.Object, assetId.ToString()))
            .ThrowsAsync(new InvalidOperationException("tracker write failed"));

        var logger = new Mock<ILogger<MultiImmichFrameLogicDelegate>>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(tracker.Object)
            .BuildServiceProvider();

        var sut = ActivatorUtilities.CreateInstance<MultiImmichFrameLogicDelegate>(
            serviceProvider,
            serverSettings.Object,
            (Func<IAccountSettings, IAccountImmichFrameLogic>)(_ => account.Object),
            logger.Object,
            selectionStrategy.Object);

        var result = await sut.GetAsset(assetId);

        Assert.That(result, Is.SameAs(expected));
        account.Verify(x => x.GetAsset(assetId, null, null), Times.Once);
    }

    [Test]
    public async Task GetAssetInfoById_WhenTrackerMisses_ReturnsAssetFromMatchingAccount()
    {
        var assetId = Guid.NewGuid();

        var accountSettingsA = new Mock<IAccountSettings>();
        accountSettingsA.SetupGet(x => x.ImmichServerUrl).Returns("https://a.example");

        var accountSettingsB = new Mock<IAccountSettings>();
        accountSettingsB.SetupGet(x => x.ImmichServerUrl).Returns("https://b.example");

        var accountA = new Mock<IAccountImmichFrameLogic>();
        accountA.SetupGet(x => x.AccountSettings).Returns(accountSettingsA.Object);
        accountA.Setup(x => x.GetAssetInfoById(assetId)).ThrowsAsync(new AssetNotFoundException());

        var expected = new AssetResponseDto { Id = assetId.ToString() };
        var accountB = new Mock<IAccountImmichFrameLogic>();
        accountB.SetupGet(x => x.AccountSettings).Returns(accountSettingsB.Object);
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(expected);

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.Accounts).Returns([accountSettingsA.Object, accountSettingsB.Object]);

        var selectionStrategy = new Mock<IAccountSelectionStrategy>();
        selectionStrategy
            .Setup(x => x.ForAsset(assetId, It.IsAny<Func<IAccountImmichFrameLogic, Task<AssetResponseDto>>>()))
            .Throws(new AssetNotFoundException());

        var tracker = new Mock<IAssetAccountTracker>();

        var logger = new Mock<ILogger<MultiImmichFrameLogicDelegate>>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(tracker.Object)
            .BuildServiceProvider();

        var sut = ActivatorUtilities.CreateInstance<MultiImmichFrameLogicDelegate>(
            serviceProvider,
            serverSettings.Object,
            (Func<IAccountSettings, IAccountImmichFrameLogic>)(settings =>
                ReferenceEquals(settings, accountSettingsA.Object) ? accountA.Object : accountB.Object),
            logger.Object,
            selectionStrategy.Object);

        var result = await sut.GetAssetInfoById(assetId);

        Assert.That(result, Is.SameAs(expected));
        Assert.That(result.ImmichServerUrl, Is.EqualTo("https://b.example"));
        tracker.Verify(x => x.RecordAssetLocation(accountB.Object, assetId.ToString()), Times.Once);
    }

    [Test]
    public async Task GetAlbumInfoById_WhenTrackerMisses_ReturnsAlbumsFromMatchingAccount()
    {
        var assetId = Guid.NewGuid();

        var accountSettingsA = new Mock<IAccountSettings>();
        accountSettingsA.SetupGet(x => x.ImmichServerUrl).Returns("https://a.example");

        var accountSettingsB = new Mock<IAccountSettings>();
        accountSettingsB.SetupGet(x => x.ImmichServerUrl).Returns("https://b.example");

        var accountA = new Mock<IAccountImmichFrameLogic>();
        accountA.SetupGet(x => x.AccountSettings).Returns(accountSettingsA.Object);
        accountA.Setup(x => x.GetAssetInfoById(assetId)).ThrowsAsync(new AssetNotFoundException());

        var expected = new[]
        {
            new AlbumResponseDto
            {
                AlbumName = "Recovered album"
            }
        };

        var accountB = new Mock<IAccountImmichFrameLogic>();
        accountB.SetupGet(x => x.AccountSettings).Returns(accountSettingsB.Object);
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(new AssetResponseDto { Id = assetId.ToString() });
        accountB.Setup(x => x.GetAlbumInfoById(assetId)).ReturnsAsync(expected);

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.Accounts).Returns([accountSettingsA.Object, accountSettingsB.Object]);

        var selectionStrategy = new Mock<IAccountSelectionStrategy>();
        selectionStrategy
            .Setup(x => x.ForAsset(assetId, It.IsAny<Func<IAccountImmichFrameLogic, Task<IEnumerable<AlbumResponseDto>>>>()))
            .Throws(new AssetNotFoundException());

        var tracker = new Mock<IAssetAccountTracker>();

        var logger = new Mock<ILogger<MultiImmichFrameLogicDelegate>>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(tracker.Object)
            .BuildServiceProvider();

        var sut = ActivatorUtilities.CreateInstance<MultiImmichFrameLogicDelegate>(
            serviceProvider,
            serverSettings.Object,
            (Func<IAccountSettings, IAccountImmichFrameLogic>)(settings =>
                ReferenceEquals(settings, accountSettingsA.Object) ? accountA.Object : accountB.Object),
            logger.Object,
            selectionStrategy.Object);

        var result = (await sut.GetAlbumInfoById(assetId)).ToList();

        Assert.That(result, Is.EqualTo(expected));
        tracker.Verify(x => x.RecordAssetLocation(accountB.Object, assetId.ToString()), Times.Once);
    }

    private static AssetResponseDto CreateAsset(string id) => new()
    {
        Id = id
    };
}
