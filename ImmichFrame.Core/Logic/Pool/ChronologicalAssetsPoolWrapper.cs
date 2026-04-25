using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.Core.Logic.Pool;

public class ChronologicalAssetsPoolWrapper(IAssetPool basePool) : IAssetPool
{
    private const int FetchMultiplier = 2;
    private const int MaxFetchCount = 1000;
    private const int GroupSize = 3;

    public async Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default)
    {
        var fetchCount = Math.Min(MaxFetchCount, requested * FetchMultiplier);
        var assets = (await basePool.GetAssets(fetchCount, ct)).ToList();

        if (assets.Count == 0)
        {
            return assets;
        }

        var datedAssets = new List<(AssetResponseDto Asset, DateTime Date)>();
        var undatedAssets = new List<AssetResponseDto>();

        foreach (var asset in assets)
        {
            if (TryGetAssetDate(asset, out var date))
            {
                datedAssets.Add((asset, date));
            }
            else
            {
                undatedAssets.Add(asset);
            }
        }

        var orderedAssets = datedAssets
            .OrderBy(x => x.Date)
            .Select(x => x.Asset)
            .Concat(undatedAssets)
            .ToList();

        var groupedAssets = orderedAssets
            .Chunk(GroupSize)
            .Select(group => group.ToList())
            .ToList();

        Shuffle(groupedAssets);

        return groupedAssets
            .SelectMany(group => group)
            .Take(requested);
    }

    public Task<long> GetAssetCount(CancellationToken ct = default) => basePool.GetAssetCount(ct);

    private static bool TryGetAssetDate(AssetResponseDto asset, out DateTime date)
    {
        if (asset.ExifInfo?.DateTimeOriginal is DateTimeOffset dateTimeOriginal)
        {
            date = dateTimeOriginal.DateTime;
            return true;
        }

        if (asset.FileCreatedAt != default)
        {
            date = asset.FileCreatedAt.DateTime;
            return true;
        }

        date = default;
        return false;
    }

    private static void Shuffle(List<List<AssetResponseDto>> groups)
    {
        for (var i = groups.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (groups[i], groups[j]) = (groups[j], groups[i]);
        }
    }
}
