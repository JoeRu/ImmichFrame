using ImmichFrame.Core.Api;
using ImmichFrame.Core.Exceptions;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic;
using ImmichFrame.Core.Logic.AccountSelection;
using ImmichFrame.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic;

[TestFixture]
public class MultiImmichFrameLogicDelegateTests
{
    [Test]
    public async Task GetAssets_PreservesSelectionOrder_WhenChronologicalGroupingEnabled()
    {
        var generalSettings = new Mock<IGeneralSettings>();
        generalSettings.SetupGet(x => x.ChronologicalImagesCount).Returns(3);

        var accountSettings = CreateAccountSettings("https://example.com");
        var accountLogic = CreateAccount(accountSettings);

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.GeneralSettings).Returns(generalSettings.Object);
        serverSettings.SetupGet(x => x.Accounts).Returns(new[] { accountSettings.Object });

        var selectionStrategy = new Mock<IAccountSelectionStrategy>();
        selectionStrategy
            .Setup(x => x.GetAssets())
            .ReturnsAsync(new List<(IAccountImmichFrameLogic, AssetResponseDto)>
            {
                (accountLogic.Object, CreateAsset("a")),
                (accountLogic.Object, CreateAsset("b")),
                (accountLogic.Object, CreateAsset("c"))
            });

        var tracker = new Mock<IAssetAccountTracker>();
        var sut = new MultiImmichFrameLogicDelegate(
            serverSettings.Object,
            _ => accountLogic.Object,
            Mock.Of<ILogger<MultiImmichFrameLogicDelegate>>(),
            selectionStrategy.Object,
            tracker.Object);

        var result = (await sut.GetAssets()).ToList();

        Assert.That(result.Select(x => x.Checksum), Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public async Task GetAsset_WhenTrackerMisses_FallsBackToMatchingAccountAndRecaches()
    {
        var assetId = Guid.NewGuid();
        var expected = new AssetResponse { FileName = "asset.jpg", ContentType = "image/jpeg", FileStream = Stream.Null };

        var accountSettingsA = CreateAccountSettings("https://a.example");
        var accountSettingsB = CreateAccountSettings("https://b.example");

        var accountA = CreateAccount(accountSettingsA);
        accountA.Setup(x => x.GetAssetInfoById(assetId)).ThrowsAsync(new AssetNotFoundException());

        var accountB = CreateAccount(accountSettingsB);
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(new AssetResponseDto { Id = assetId });
        accountB.Setup(x => x.GetAsset(assetId, It.IsAny<AssetTypeEnum?>(), It.IsAny<string?>())).ReturnsAsync(expected);

        var tracker = new Mock<IAssetAccountTracker>();
        var sut = CreateSut(assetId, accountSettingsA, accountA, accountSettingsB, accountB, tracker, assetLookupThrows: true);

        var result = await sut.GetAsset(assetId);

        Assert.That(result, Is.EqualTo(expected));
        tracker.Verify(x => x.RecordAssetLocation(accountB.Object, assetId), Times.Once);
        accountA.Verify(x => x.GetAssetInfoById(assetId), Times.Once);
        accountB.Verify(x => x.GetAssetInfoById(assetId), Times.Once);
        accountB.Verify(x => x.GetAsset(assetId, It.IsAny<AssetTypeEnum?>(), It.IsAny<string?>()), Times.Once);
    }

    [Test]
    public async Task GetAssetInfoById_WhenTrackerMisses_ReturnsAssetFromMatchingAccount()
    {
        var assetId = Guid.NewGuid();

        var accountSettingsA = CreateAccountSettings("https://a.example");
        var accountSettingsB = CreateAccountSettings("https://b.example");

        var accountA = CreateAccount(accountSettingsA);
        accountA.Setup(x => x.GetAssetInfoById(assetId)).ThrowsAsync(new AssetNotFoundException());

        var expected = new AssetResponseDto { Id = assetId };
        var accountB = CreateAccount(accountSettingsB);
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(expected);

        var tracker = new Mock<IAssetAccountTracker>();
        var sut = CreateSut(assetId, accountSettingsA, accountA, accountSettingsB, accountB, tracker, infoLookupThrows: true);

        var result = await sut.GetAssetInfoById(assetId);

        Assert.That(result, Is.SameAs(expected));
        Assert.That(result.ImmichServerUrl, Is.EqualTo("https://b.example"));
        tracker.Verify(x => x.RecordAssetLocation(accountB.Object, assetId), Times.Once);
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
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(new AssetResponseDto { Id = assetId });
        accountB.Setup(x => x.GetAlbumInfoById(assetId)).ReturnsAsync(expected);

        var tracker = new Mock<IAssetAccountTracker>();
        var sut = CreateSut(assetId, accountSettingsA, accountA, accountSettingsB, accountB, tracker, albumLookupThrows: true);

        var result = await sut.GetAlbumInfoById(assetId);

        Assert.That(result, Is.SameAs(expected));
        tracker.Verify(x => x.RecordAssetLocation(accountB.Object, assetId), Times.Once);
    }

    [Test]
    public async Task GetAsset_WhenRecacheFails_ReturnsResolvedAssetAndLogsWarning()
    {
        var assetId = Guid.NewGuid();
        var recacheException = new InvalidOperationException("recache failed");
        var expected = new AssetResponse { FileName = "asset.jpg", ContentType = "image/jpeg", FileStream = Stream.Null };

        var accountSettingsA = CreateAccountSettings("https://a.example");
        var accountSettingsB = CreateAccountSettings("https://b.example");

        var accountA = CreateAccount(accountSettingsA);
        accountA.Setup(x => x.GetAssetInfoById(assetId)).ThrowsAsync(new AssetNotFoundException());

        var accountB = CreateAccount(accountSettingsB);
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(new AssetResponseDto { Id = assetId });
        accountB.Setup(x => x.GetAsset(assetId, It.IsAny<AssetTypeEnum?>(), It.IsAny<string?>())).ReturnsAsync(expected);

        var tracker = new Mock<IAssetAccountTracker>();
        tracker.Setup(x => x.RecordAssetLocation(accountB.Object, assetId)).ThrowsAsync(recacheException);

        var logger = new Mock<ILogger<MultiImmichFrameLogicDelegate>>();
        var sut = CreateSut(assetId, accountSettingsA, accountA, accountSettingsB, accountB, tracker, logger, assetLookupThrows: true);

        var result = await sut.GetAsset(assetId);

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
    public async Task GetAsset_WhenProbeReturnsNull_ContinuesFallbackUntilMatchingAccount()
    {
        var assetId = Guid.NewGuid();
        var expected = new AssetResponse { FileName = "asset.jpg", ContentType = "image/jpeg", FileStream = Stream.Null };

        var accountSettingsA = CreateAccountSettings("https://a.example");
        var accountSettingsB = CreateAccountSettings("https://b.example");
        var accountSettingsC = CreateAccountSettings("https://c.example");

        var accountA = CreateAccount(accountSettingsA);
        accountA.Setup(x => x.GetAssetInfoById(assetId)).ThrowsAsync(new AssetNotFoundException());

        var accountB = CreateAccount(accountSettingsB);
        accountB.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync((AssetResponseDto)null!);

        var accountC = CreateAccount(accountSettingsC);
        accountC.Setup(x => x.GetAssetInfoById(assetId)).ReturnsAsync(new AssetResponseDto { Id = assetId });
        accountC.Setup(x => x.GetAsset(assetId, It.IsAny<AssetTypeEnum?>(), It.IsAny<string?>())).ReturnsAsync(expected);

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.Accounts).Returns([accountSettingsA.Object, accountSettingsB.Object, accountSettingsC.Object]);

        var selectionStrategy = new Mock<IAccountSelectionStrategy>();
        selectionStrategy
            .Setup(x => x.ForAsset(assetId, It.IsAny<Func<IAccountImmichFrameLogic, Task<AssetResponse>>>()))
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

        var result = await sut.GetAsset(assetId);

        Assert.That(result, Is.EqualTo(expected));
        accountB.Verify(x => x.GetAsset(assetId, It.IsAny<AssetTypeEnum?>(), It.IsAny<string?>()), Times.Never);
        accountC.Verify(x => x.GetAsset(assetId, It.IsAny<AssetTypeEnum?>(), It.IsAny<string?>()), Times.Once);
        tracker.Verify(x => x.RecordAssetLocation(accountC.Object, assetId), Times.Once);
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

    private static AssetResponseDto CreateAsset(string checksum)
    {
        return new AssetResponseDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Type = AssetTypeEnum.IMAGE,
            OriginalPath = "/photo.jpg",
            OriginalFileName = "photo.jpg",
            Checksum = checksum,
            Thumbhash = "thumb",
            Visibility = AssetVisibility.Timeline,
            Width = 1,
            Height = 1,
            HasMetadata = true,
            IsFavorite = false,
            FileCreatedAt = DateTimeOffset.UtcNow,
            FileModifiedAt = DateTimeOffset.UtcNow,
            LocalDateTime = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static MultiImmichFrameLogicDelegate CreateSut(
        Guid assetId,
        Mock<IAccountSettings> accountSettingsA,
        Mock<IAccountImmichFrameLogic> accountA,
        Mock<IAccountSettings> accountSettingsB,
        Mock<IAccountImmichFrameLogic> accountB,
        Mock<IAssetAccountTracker> tracker,
        Mock<ILogger<MultiImmichFrameLogicDelegate>>? logger = null,
        bool assetLookupThrows = false,
        bool infoLookupThrows = false,
        bool albumLookupThrows = false)
    {
        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.Accounts).Returns([accountSettingsA.Object, accountSettingsB.Object]);

        var selectionStrategy = new Mock<IAccountSelectionStrategy>();
        if (assetLookupThrows)
        {
            selectionStrategy
                .Setup(x => x.ForAsset(assetId, It.IsAny<Func<IAccountImmichFrameLogic, Task<AssetResponse>>>()))
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
