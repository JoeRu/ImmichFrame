using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.Pool;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic.Pool;

[TestFixture]
public class ChronologicalAssetsPoolWrapperTests
{
    private Mock<IAssetPool> _basePool = null!;
    private Mock<IGeneralSettings> _generalSettings = null!;
    private ChronologicalAssetsPoolWrapper _wrapper = null!;

    [SetUp]
    public void Setup()
    {
        _basePool = new Mock<IAssetPool>();
        _generalSettings = new Mock<IGeneralSettings>();
        _generalSettings.SetupGet(x => x.ChronologicalImagesCount).Returns(3);
        _wrapper = new ChronologicalAssetsPoolWrapper(_basePool.Object, _generalSettings.Object);
    }

    [Test]
    public async Task GetAssets_WhenDisabled_UsesBasePool()
    {
        var assets = CreateAssets(
            new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero));
        _generalSettings.SetupGet(x => x.ChronologicalImagesCount).Returns(0);
        _basePool.Setup(x => x.GetAssets(2, It.IsAny<CancellationToken>())).ReturnsAsync(assets);

        var result = (await _wrapper.GetAssets(2)).ToList();

        Assert.That(result.Select(x => x.Id), Is.EqualTo(assets.Select(x => x.Id)));
        _basePool.Verify(x => x.GetAssets(2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetAssets_WhenEnabled_FetchesExpandedBatch()
    {
        _basePool.Setup(x => x.GetAssets(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAssets(
                new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2024, 1, 4, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero)));

        await _wrapper.GetAssets(5);

        _basePool.Verify(x => x.GetAssets(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetAssets_PreservesChronologicalOrderWithinEachSet()
    {
        var assets = CreateAssets(
            new DateTimeOffset(2024, 1, 6, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 4, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _basePool.Setup(x => x.GetAssets(12, It.IsAny<CancellationToken>())).ReturnsAsync(assets);

        var result = (await _wrapper.GetAssets(6)).ToList();

        Assert.That(result, Has.Count.EqualTo(6));
        var allDates = result.Select(x => x.ExifInfo!.DateTimeOriginal!.Value).OrderBy(x => x).ToList();
        Assert.That(allDates, Is.EqualTo(assets.Select(x => x.ExifInfo!.DateTimeOriginal!.Value).OrderBy(x => x).ToList()));

        for (var i = 0; i < result.Count; i += 3)
        {
            var currentSet = result.Skip(i).Take(3).ToList();
            var orderedSet = currentSet.Select(x => x.ExifInfo!.DateTimeOriginal!.Value).OrderBy(x => x).ToList();
            Assert.That(currentSet.Select(x => x.ExifInfo!.DateTimeOriginal!.Value).ToList(), Is.EqualTo(orderedSet));
        }
    }

    [Test]
    public async Task GetAssets_AppendsUndatedAssetsAfterDatedAssetsBeforeGrouping()
    {
        var datedOld = CreateAsset(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var datedNew = CreateAsset(new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero));
        var undated = CreateAsset(null, DateTimeOffset.MinValue);
        _basePool.Setup(x => x.GetAssets(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { undated, datedNew, datedOld });

        var result = (await _wrapper.GetAssets(3)).ToList();

        Assert.That(result.Select(x => x.Id), Does.Contain(datedOld.Id));
        Assert.That(result.Select(x => x.Id), Does.Contain(datedNew.Id));
        Assert.That(result.Select(x => x.Id), Does.Contain(undated.Id));
    }

    private static List<AssetResponseDto> CreateAssets(params DateTimeOffset[] dates) => dates.Select(date => CreateAsset(date)).ToList();

    private static AssetResponseDto CreateAsset(DateTimeOffset? exifDate, DateTimeOffset? fileCreatedAt = null)
    {
        return new AssetResponseDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Type = AssetTypeEnum.IMAGE,
            OriginalPath = "/photo.jpg",
            OriginalFileName = "photo.jpg",
            Checksum = Guid.NewGuid().ToString("N"),
            Thumbhash = "thumb",
            Visibility = AssetVisibility.Timeline,
            Width = 1,
            Height = 1,
            HasMetadata = true,
            IsFavorite = false,
            FileCreatedAt = fileCreatedAt ?? exifDate ?? DateTimeOffset.UtcNow,
            FileModifiedAt = fileCreatedAt ?? exifDate ?? DateTimeOffset.UtcNow,
            LocalDateTime = fileCreatedAt ?? exifDate ?? DateTimeOffset.UtcNow,
            CreatedAt = fileCreatedAt ?? exifDate ?? DateTimeOffset.UtcNow,
            UpdatedAt = fileCreatedAt ?? exifDate ?? DateTimeOffset.UtcNow,
            ExifInfo = exifDate.HasValue ? new ExifResponseDto { DateTimeOriginal = exifDate.Value } : null
        };
    }
}
