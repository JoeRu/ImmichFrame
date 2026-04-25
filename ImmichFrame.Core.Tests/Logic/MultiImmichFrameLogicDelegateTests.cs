using ImmichFrame.Core.Api;
using ImmichFrame.Core.Exceptions;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic;
using ImmichFrame.Core.Logic.AccountSelection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic;

[TestFixture]
public class MultiImmichFrameLogicDelegateTests
{
    [Test]
    public async Task GetImage_WhenTrackerMisses_FallsBackToMatchingAccountAndRecaches()
    {
        var assetId = Guid.NewGuid();
        var expected = ("asset.jpg", "image/jpeg", Stream.Null);

        var accountSettingsA = CreateAccountSettings("https://a.example");
        var accountSettingsB = CreateAccountSettings("https://b.example");

        var accountA = CreateAccount(accountSettingsA);
        accountA.Setup(x => x.GetAssetInfoById(assetId)).ThrowsAsync(new AssetNotFoundException());

        var accountB = CreateAccount(accountSettingsB);
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(new AssetResponseDto { Id = assetId.ToString() });
        accountB.Setup(x => x.GetImage(assetId)).ReturnsAsync(expected);

        var tracker = new Mock<IAssetAccountTracker>();
        var sut = CreateSut(assetId, accountSettingsA, accountA, accountSettingsB, accountB, tracker, imageLookupThrows: true);

        var result = await sut.GetImage(assetId);

        Assert.That(result, Is.EqualTo(expected));
        tracker.Verify(x => x.RecordAssetLocation(accountB.Object, assetId.ToString()), Times.Once);
        accountA.Verify(x => x.GetAssetInfoById(assetId), Times.Once);
        accountB.Verify(x => x.GetAssetInfoById(assetId), Times.Once);
        accountB.Verify(x => x.GetImage(assetId), Times.Once);
    }

    [Test]
    public async Task GetAssetInfoById_WhenTrackerMisses_ReturnsAssetFromMatchingAccount()
    {
        var assetId = Guid.NewGuid();

        var accountSettingsA = CreateAccountSettings("https://a.example");
        var accountSettingsB = CreateAccountSettings("https://b.example");

        var accountA = CreateAccount(accountSettingsA);
        accountA.Setup(x => x.GetAssetInfoById(assetId)).ThrowsAsync(new AssetNotFoundException());

        var expected = new AssetResponseDto { Id = assetId.ToString() };
        var accountB = CreateAccount(accountSettingsB);
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(expected);

        var tracker = new Mock<IAssetAccountTracker>();
        var sut = CreateSut(assetId, accountSettingsA, accountA, accountSettingsB, accountB, tracker, infoLookupThrows: true);

        var result = await sut.GetAssetInfoById(assetId);

        Assert.That(result, Is.SameAs(expected));
        Assert.That(result.ImmichServerUrl, Is.EqualTo("https://b.example"));
        tracker.Verify(x => x.RecordAssetLocation(accountB.Object, assetId.ToString()), Times.Once);
    }

    [Test]
    public async Task GetAlbumInfoById_WhenTrackerMisses_ReturnsAlbumsFromMatchingAccount()
    {
        var assetId = Guid.NewGuid();

        var accountSettingsA = CreateAccountSettings("https://a.example");
        var accountSettingsB = CreateAccountSettings("https://b.example");

        var accountA = CreateAccount(accountSettingsA);
        accountA.Setup(x => x.GetAssetInfoById(assetId)).ThrowsAsync(new AssetNotFoundException());

        var expected = new[] { new AlbumResponseDto { AssetCount = 1 } };
        var accountB = CreateAccount(accountSettingsB);
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(new AssetResponseDto { Id = assetId.ToString() });
        accountB.Setup(x => x.GetAlbumInfoById(assetId)).ReturnsAsync(expected);

        var tracker = new Mock<IAssetAccountTracker>();
        var sut = CreateSut(assetId, accountSettingsA, accountA, accountSettingsB, accountB, tracker, albumLookupThrows: true);

        var result = await sut.GetAlbumInfoById(assetId);

        Assert.That(result, Is.SameAs(expected));
        tracker.Verify(x => x.RecordAssetLocation(accountB.Object, assetId.ToString()), Times.Once);
    }

    [Test]
    public async Task GetImage_WhenRecacheFails_ReturnsResolvedImageAndLogsWarning()
    {
        var assetId = Guid.NewGuid();
        var recacheException = new InvalidOperationException("recache failed");
        var expected = ("asset.jpg", "image/jpeg", Stream.Null);

        var accountSettingsA = CreateAccountSettings("https://a.example");
        var accountSettingsB = CreateAccountSettings("https://b.example");

        var accountA = CreateAccount(accountSettingsA);
        accountA.Setup(x => x.GetAssetInfoById(assetId)).ThrowsAsync(new AssetNotFoundException());

        var accountB = CreateAccount(accountSettingsB);
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(new AssetResponseDto { Id = assetId.ToString() });
        accountB.Setup(x => x.GetImage(assetId)).ReturnsAsync(expected);

        var tracker = new Mock<IAssetAccountTracker>();
        tracker.Setup(x => x.RecordAssetLocation(accountB.Object, assetId.ToString())).ThrowsAsync(recacheException);

        var logger = new Mock<ILogger<MultiImmichFrameLogicDelegate>>();
        var sut = CreateSut(assetId, accountSettingsA, accountA, accountSettingsB, accountB, tracker, logger, imageLookupThrows: true);

        var result = await sut.GetImage(assetId);

        Assert.That(result, Is.EqualTo(expected));
        logger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Failed to recache asset location")),
                recacheException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task GetImage_WhenProbeReturnsNull_ContinuesFallbackUntilMatchingAccount()
    {
        var assetId = Guid.NewGuid();
        var expected = ("asset.jpg", "image/jpeg", Stream.Null);

        var accountSettingsA = CreateAccountSettings("https://a.example");
        var accountSettingsB = CreateAccountSettings("https://b.example");
        var accountSettingsC = CreateAccountSettings("https://c.example");

        var accountA = CreateAccount(accountSettingsA);
        accountA.Setup(x => x.GetAssetInfoById(assetId)).ThrowsAsync(new AssetNotFoundException());

        var accountB = CreateAccount(accountSettingsB);
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync((AssetResponseDto)null!);

        var accountC = CreateAccount(accountSettingsC);
        accountC.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(new AssetResponseDto { Id = assetId.ToString() });
        accountC.Setup(x => x.GetImage(assetId)).ReturnsAsync(expected);

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.Accounts).Returns([accountSettingsA.Object, accountSettingsB.Object, accountSettingsC.Object]);

        var selectionStrategy = new Mock<IAccountSelectionStrategy>();
        selectionStrategy
            .Setup(x => x.ForAsset(assetId, It.IsAny<Func<IAccountImmichFrameLogic, Task<(string fileName, string ContentType, Stream fileStream)>>>()))
            .Throws(new AssetNotFoundException());

        var tracker = new Mock<IAssetAccountTracker>();
        var sut = new MultiImmichFrameLogicDelegate(
            serverSettings.Object,
            settings => ReferenceEquals(settings, accountSettingsA.Object)
                ? accountA.Object
                : ReferenceEquals(settings, accountSettingsB.Object)
                    ? accountB.Object
                    : accountC.Object,
            Mock.Of<ILogger<MultiImmichFrameLogicDelegate>>(),
            selectionStrategy.Object,
            tracker.Object);

        var result = await sut.GetImage(assetId);

        Assert.That(result, Is.EqualTo(expected));
        accountB.Verify(x => x.GetImage(assetId), Times.Never);
        accountC.Verify(x => x.GetImage(assetId), Times.Once);
        tracker.Verify(x => x.RecordAssetLocation(accountC.Object, assetId.ToString()), Times.Once);
    }

    private static Mock<IAccountSettings> CreateAccountSettings(string url)
    {
        var settings = new Mock<IAccountSettings>();
        settings.SetupGet(x => x.ImmichServerUrl).Returns(url);
        return settings;
    }

    private static Mock<IAccountImmichFrameLogic> CreateAccount(Mock<IAccountSettings> settings)
    {
        var account = new Mock<IAccountImmichFrameLogic>();
        account.SetupGet(x => x.AccountSettings).Returns(settings.Object);
        return account;
    }

    private static MultiImmichFrameLogicDelegate CreateSut(
        Guid assetId,
        Mock<IAccountSettings> accountSettingsA,
        Mock<IAccountImmichFrameLogic> accountA,
        Mock<IAccountSettings> accountSettingsB,
        Mock<IAccountImmichFrameLogic> accountB,
        Mock<IAssetAccountTracker> tracker,
        Mock<ILogger<MultiImmichFrameLogicDelegate>>? logger = null,
        bool imageLookupThrows = false,
        bool infoLookupThrows = false,
        bool albumLookupThrows = false)
    {
        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.Accounts).Returns([accountSettingsA.Object, accountSettingsB.Object]);

        var selectionStrategy = new Mock<IAccountSelectionStrategy>();
        if (imageLookupThrows)
        {
            selectionStrategy
                .Setup(x => x.ForAsset(assetId, It.IsAny<Func<IAccountImmichFrameLogic, Task<(string fileName, string ContentType, Stream fileStream)>>>()))
                .Throws(new AssetNotFoundException());
        }

        if (infoLookupThrows)
        {
            selectionStrategy
                .Setup(x => x.ForAsset(assetId, It.IsAny<Func<IAccountImmichFrameLogic, Task<AssetResponseDto>>>()))
                .Throws(new AssetNotFoundException());
        }

        if (albumLookupThrows)
        {
            selectionStrategy
                .Setup(x => x.ForAsset(assetId, It.IsAny<Func<IAccountImmichFrameLogic, Task<IEnumerable<AlbumResponseDto>>>>()))
                .Throws(new AssetNotFoundException());
        }

        return new MultiImmichFrameLogicDelegate(
            serverSettings.Object,
            settings => ReferenceEquals(settings, accountSettingsA.Object) ? accountA.Object : accountB.Object,
            (logger ?? new Mock<ILogger<MultiImmichFrameLogicDelegate>>()).Object,
            selectionStrategy.Object,
            tracker.Object);
    }
}
