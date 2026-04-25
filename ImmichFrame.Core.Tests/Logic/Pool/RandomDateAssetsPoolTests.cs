using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.Pool;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic.Pool;

[TestFixture]
public class RandomDateAssetsPoolTests
{
    private Mock<IApiCache> _apiCache = null!;
    private Mock<ImmichApi> _immichApi = null!;
    private Mock<IAccountSettings> _accountSettings = null!;

    [SetUp]
    public void SetUp()
    {
        _apiCache = new Mock<IApiCache>();
        _immichApi = new Mock<ImmichApi>("", null!);
        _accountSettings = new Mock<IAccountSettings>();

        _accountSettings.SetupGet(x => x.ShowArchived).Returns(false);
        _accountSettings.SetupGet(x => x.ShowVideos).Returns(false);
        _accountSettings.SetupGet(x => x.ImagesFromDate).Returns((DateTime?)null);
        _accountSettings.SetupGet(x => x.ImagesFromDays).Returns((int?)null);
        _accountSettings.SetupGet(x => x.ImagesUntilDate).Returns((DateTime?)null);
        _accountSettings.SetupGet(x => x.Rating).Returns((int?)null);
    }

    [Test]
    public async Task GetAssets_WithClusteredDateSearches_ReturnsRequestedAssets()
    {
        var oldestAsset = CreateAsset("oldest", new DateTime(2020, 1, 1));
        var youngestAsset = CreateAsset("youngest", new DateTime(2024, 12, 31));
        var randomAssets = new List<AssetResponseDto>
        {
            CreateAsset("random-1", new DateTime(2022, 6, 15)),
            CreateAsset("random-2", new DateTime(2022, 6, 16)),
            CreateAsset("random-3", new DateTime(2022, 6, 17))
        };

        _immichApi.Setup(x => x.SearchAssetsAsync(
                It.Is<MetadataSearchDto>(dto => dto.Order == AssetOrder.Asc && dto.Size == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSearchResult(new[] { oldestAsset }, 1));

        _immichApi.Setup(x => x.SearchAssetsAsync(
                It.Is<MetadataSearchDto>(dto => dto.Order == AssetOrder.Desc && dto.Size == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSearchResult(new[] { youngestAsset }, 1));

        _immichApi.Setup(x => x.SearchAssetsAsync(
                It.Is<MetadataSearchDto>(dto => dto.Size == 1 && dto.TakenAfter.HasValue && dto.TakenBefore.HasValue),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSearchResult(Array.Empty<AssetResponseDto>(), 10));

        _immichApi.Setup(x => x.SearchAssetsAsync(
                It.Is<MetadataSearchDto>(dto => dto.Size > 1 && dto.TakenAfter.HasValue && dto.TakenBefore.HasValue),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSearchResult(randomAssets, randomAssets.Count));

        _immichApi.Setup(x => x.SearchAssetsAsync(
                It.Is<MetadataSearchDto>(dto => !dto.TakenAfter.HasValue && !dto.TakenBefore.HasValue && dto.Size >= 1000),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSearchResult(Array.Empty<AssetResponseDto>(), 0));

        var pool = new RandomDateAssetsPool(_apiCache.Object, _immichApi.Object, _accountSettings.Object, enableClusterCaching: false);
        pool.ConfigureAssetsPerRandomDate(3);

        var result = (await pool.GetAssets(3)).ToList();

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result.Select(x => x.Id), Is.EquivalentTo(new[] { "random-1", "random-2", "random-3" }));
    }

    [Test]
    public async Task GetAssets_WhenClusteredDateSearchesAreEmpty_FallsBackToBroadSearch()
    {
        var oldestAsset = CreateAsset("oldest", new DateTime(2020, 1, 1));
        var youngestAsset = CreateAsset("youngest", new DateTime(2024, 12, 31));
        var fallbackAssets = new List<AssetResponseDto>
        {
            CreateAsset("fallback-1", new DateTime(2023, 1, 1)),
            CreateAsset("fallback-2", new DateTime(2023, 1, 2)),
            CreateAsset("fallback-3", new DateTime(2023, 1, 3))
        };

        _immichApi.Setup(x => x.SearchAssetsAsync(
                It.Is<MetadataSearchDto>(dto => dto.Order == AssetOrder.Asc && dto.Size == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSearchResult(new[] { oldestAsset }, 1));

        _immichApi.Setup(x => x.SearchAssetsAsync(
                It.Is<MetadataSearchDto>(dto => dto.Order == AssetOrder.Desc && dto.Size == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSearchResult(new[] { youngestAsset }, 1));

        _immichApi.Setup(x => x.SearchAssetsAsync(
                It.Is<MetadataSearchDto>(dto => dto.Size == 1 && dto.TakenAfter.HasValue && dto.TakenBefore.HasValue),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSearchResult(Array.Empty<AssetResponseDto>(), 10));

        _immichApi.Setup(x => x.SearchAssetsAsync(
                It.Is<MetadataSearchDto>(dto => dto.Size > 1 && dto.TakenAfter.HasValue && dto.TakenBefore.HasValue),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSearchResult(Array.Empty<AssetResponseDto>(), 0));

        _immichApi.Setup(x => x.SearchAssetsAsync(
                It.Is<MetadataSearchDto>(dto => !dto.TakenAfter.HasValue && !dto.TakenBefore.HasValue && dto.Size >= 1000),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSearchResult(fallbackAssets, fallbackAssets.Count));

        var pool = new RandomDateAssetsPool(_apiCache.Object, _immichApi.Object, _accountSettings.Object, enableClusterCaching: false);
        pool.ConfigureAssetsPerRandomDate(3);

        var result = (await pool.GetAssets(3)).ToList();

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result.Select(x => x.Id), Is.EquivalentTo(new[] { "fallback-1", "fallback-2", "fallback-3" }));
    }

    private static AssetResponseDto CreateAsset(string id, DateTime date) => new()
    {
        Id = id,
        Type = AssetTypeEnum.IMAGE,
        IsArchived = false,
        ExifInfo = new ExifResponseDto
        {
            DateTimeOriginal = date
        },
        FileCreatedAt = date
    };

    private static SearchResponseDto CreateSearchResult(IEnumerable<AssetResponseDto> assets, int total) => new()
    {
        Assets = new SearchAssetResponseDto
        {
            Items = assets.ToList(),
            Total = total
        }
    };
}
