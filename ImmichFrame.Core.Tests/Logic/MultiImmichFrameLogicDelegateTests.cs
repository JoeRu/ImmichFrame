using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic;
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

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.GeneralSettings).Returns(generalSettings.Object);
        serverSettings.SetupGet(x => x.Accounts).Returns(new[] { CreateAccountSettings("https://example.com") });

        var selectionStrategy = new Mock<IAccountSelectionStrategy>();
        selectionStrategy
            .Setup(x => x.GetAssets())
            .ReturnsAsync(new List<(IAccountImmichFrameLogic, AssetResponseDto)>
            {
                (CreateAccountLogic("https://example.com"), CreateAsset("a")),
                (CreateAccountLogic("https://example.com"), CreateAsset("b")),
                (CreateAccountLogic("https://example.com"), CreateAsset("c"))
            });

        var sut = new MultiImmichFrameLogicDelegate(
            serverSettings.Object,
            _ => throw new NotSupportedException(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<MultiImmichFrameLogicDelegate>>(),
            selectionStrategy.Object);

        var result = (await sut.GetAssets()).ToList();

        Assert.That(result.Select(x => x.Checksum), Is.EqualTo(new[] { "a", "b", "c" }));
    }

    private static IAccountSettings CreateAccountSettings(string url)
    {
        var account = new Mock<IAccountSettings>();
        account.SetupGet(x => x.ImmichServerUrl).Returns(url);
        account.SetupGet(x => x.ApiKey).Returns("key");
        account.SetupGet(x => x.ApiKeyFile).Returns((string?)null);
        account.SetupGet(x => x.Albums).Returns(new List<Guid>());
        account.SetupGet(x => x.ExcludedAlbums).Returns(new List<Guid>());
        account.SetupGet(x => x.People).Returns(new List<Guid>());
        account.SetupGet(x => x.Tags).Returns(new List<string>());
        return account.Object;
    }

    private static IAccountImmichFrameLogic CreateAccountLogic(string url)
    {
        var logic = new Mock<IAccountImmichFrameLogic>();
        logic.SetupGet(x => x.AccountSettings).Returns(CreateAccountSettings(url));
        return logic.Object;
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
}
