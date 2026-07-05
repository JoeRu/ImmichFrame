using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.Core.Logic.Pool;

/// <summary>
/// Wraps an IAssetPool to provide chronological asset grouping functionality.
/// This wrapper organizes assets into chronological sets based on their capture dates,
/// while randomizing the order of sets to maintain variety in display. Assets without
/// date information are preserved and placed after chronologically ordered assets.
/// Fetches assets in configurable batch sizes for optimal chronological sorting performance.
/// </summary>
public class ChronologicalAssetsPoolWrapper(IAssetPool basePool, IGeneralSettings generalSettings) : IAssetPool
{
    private const int DefaultFetchMultiplier = 2;
    private const int MaxFetchCount = 1000;

    public async Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default)
    {
        var chronologicalCount = generalSettings.ChronologicalImagesCount;
        if (chronologicalCount <= 0)
            return await basePool.GetAssets(requested, ct);

        var fetchCount = Math.Min(MaxFetchCount, requested * DefaultFetchMultiplier);
        var assets = (await basePool.GetAssets(fetchCount, ct)).ToList();
        if (assets.Count == 0)
            return assets;

        var (datedAssets, undatedAssets) = SeparateDateAndNonDateAssets(assets);
        var orderedAssets = SortAssetsChronologically(datedAssets).Concat(undatedAssets).ToList();
        var groupedAssets = CreateChronologicalSets(orderedAssets, chronologicalCount);

        return RandomizeSets(groupedAssets).SelectMany(set => set).Take(requested);
    }

    public Task<long> GetAssetCount(CancellationToken ct = default) => basePool.GetAssetCount(ct);

    private static (List<AssetResponseDto> datedAssets, List<AssetResponseDto> undatedAssets) SeparateDateAndNonDateAssets(
        IEnumerable<AssetResponseDto> assets)
    {
        var datedAssets = new List<AssetResponseDto>();
        var undatedAssets = new List<AssetResponseDto>();

        foreach (var asset in assets)
        {
            if (TryGetAssetDate(asset, out _))
                datedAssets.Add(asset);
            else
                undatedAssets.Add(asset);
        }

        return (datedAssets, undatedAssets);
    }

    private static List<AssetResponseDto> SortAssetsChronologically(IEnumerable<AssetResponseDto> assets)
    {
        return assets
            .Select(asset => (asset, hasDate: TryGetAssetDate(asset, out var date), date))
            .Where(x => x.hasDate)
            .OrderBy(x => x.date)
            .Select(x => x.asset)
            .ToList();
    }

    private static bool TryGetAssetDate(AssetResponseDto asset, out DateTimeOffset date)
    {
        if (asset.ExifInfo?.DateTimeOriginal is { } dateTimeOriginal)
        {
            date = dateTimeOriginal;
            return true;
        }

        if (asset.FileCreatedAt != default)
        {
            date = asset.FileCreatedAt;
            return true;
        }

        date = default;
        return false;
    }

    private static List<List<AssetResponseDto>> CreateChronologicalSets(IReadOnlyList<AssetResponseDto> assets, int setSize)
    {
        var sets = new List<List<AssetResponseDto>>();
        var currentSet = new List<AssetResponseDto>(setSize);

        foreach (var asset in assets)
        {
            currentSet.Add(asset);
            if (currentSet.Count < setSize)
                continue;

            sets.Add(currentSet);
            currentSet = new List<AssetResponseDto>(setSize);
        }

        if (currentSet.Count > 0)
            sets.Add(currentSet);

        return sets;
    }

    private static List<List<AssetResponseDto>> RandomizeSets(IEnumerable<List<AssetResponseDto>> sets)
    {
        var setsList = sets.ToList();
        var random = Random.Shared;

        for (var i = setsList.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (setsList[i], setsList[j]) = (setsList[j], setsList[i]);
        }

        return setsList;
    }
}
