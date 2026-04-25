using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.Pool;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic.Pool;

[TestFixture]
public class ChronologicalAssetsPoolWrapperTests
{
    [Test]
    public async Task GetAssets_GroupsAssetsInChronologicalTriples()
    {
        var basePool = new Mock<IAssetPool>();
        var assets = new[]
        {
            CreateAsset("asset-4", new DateTime(2024, 1, 4)),
            CreateAsset("asset-2", new DateTime(2024, 1, 2)),
            CreateAsset("asset-6", new DateTime(2024, 1, 6)),
            CreateAsset("asset-1", new DateTime(2024, 1, 1)),
            CreateAsset("asset-5", new DateTime(2024, 1, 5)),
            CreateAsset("asset-3", new DateTime(2024, 1, 3))
        };

        basePool.Setup(x => x.GetAssets(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assets);

        var wrapper = new ChronologicalAssetsPoolWrapper(basePool.Object);

        var result = (await wrapper.GetAssets(6)).ToList();

        Assert.That(result, Has.Count.EqualTo(6));

        var groups = result
            .Chunk(3)
            .Select(group => group.Select(asset => asset.Id).ToArray())
            .ToArray();

        Assert.That(groups, Has.Length.EqualTo(2));
        Assert.That(groups, Has.Some.EqualTo(new[] { "asset-1", "asset-2", "asset-3" }));
        Assert.That(groups, Has.Some.EqualTo(new[] { "asset-4", "asset-5", "asset-6" }));
    }

    private static AssetResponseDto CreateAsset(string id, DateTime date) => new()
    {
        Id = id,
        ExifInfo = new ExifResponseDto
        {
            DateTimeOriginal = date
        }
    };
}
