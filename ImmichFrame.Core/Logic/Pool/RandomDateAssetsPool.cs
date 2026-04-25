using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.Core.Logic.Pool;

public class PhotoCluster
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int PhotoCount { get; set; }
    public double Weight { get; set; }
    public bool IsSparse { get; set; }
}

public class RandomDateAssetsPool : IAssetPool
{
    private readonly IApiCache _apiCache;
    private readonly ImmichApi _immichApi;
    private readonly IAccountSettings _accountSettings;
    private readonly bool _enableClusterCaching;
    private readonly Random _random = Random.Shared;
    private readonly HashSet<string> _selectedAssetIds = new();

    private const int SparseClusterThreshold = 5;
    private const int MaxClusterRangeYears = 30;
    private const string ClusterCachePrefix = "RandomDatePool:Clusters";

    private int _requestedAssetCount = 50;
    private int _assetsPerRandomDate = 10;
    private bool _clustersInitialized;
    private List<PhotoCluster>? _photoClusters;

    public RandomDateAssetsPool(IApiCache apiCache, ImmichApi immichApi, IAccountSettings accountSettings, bool enableClusterCaching = true)
    {
        _apiCache = apiCache;
        _immichApi = immichApi;
        _accountSettings = accountSettings;
        _enableClusterCaching = enableClusterCaching;
    }

    public async Task<long> GetAssetCount(CancellationToken ct = default)
    {
        var cacheKey = $"{nameof(RandomDateAssetsPool)}:stats:v1:archived={_accountSettings.ShowArchived}:videos={_accountSettings.ShowVideos}";
        var stats = await _apiCache.GetOrAddAsync(cacheKey,
            () => _immichApi.GetAssetStatisticsAsync(null, _accountSettings.ShowArchived, null, ct));

        return _accountSettings.ShowVideos ? stats.Images + stats.Videos : stats.Images;
    }

    public async Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default)
    {
        _selectedAssetIds.Clear();
        _requestedAssetCount = requested;

        var assets = await LoadAssetsInternal(ct);
        var excludedAlbumAssets = await AssetHelper.GetExcludedAlbumAssets(_immichApi, _accountSettings, ct);

        return assets
            .ApplyAccountFilters(_accountSettings, excludedAlbumAssets)
            .Take(requested);
    }

    public void ConfigureAssetsPerRandomDate(int assetsPerDate)
    {
        _assetsPerRandomDate = Math.Max(1, assetsPerDate);
    }

    private async Task<IEnumerable<AssetResponseDto>> LoadAssetsInternal(CancellationToken ct = default)
    {
        var (oldestAsset, youngestAsset) = await GetOldestAndYoungestAssetsAsync(ct);
        if (oldestAsset == null || youngestAsset == null)
        {
            return Enumerable.Empty<AssetResponseDto>();
        }

        var oldestDate = GetAssetDate(oldestAsset);
        var youngestDate = GetAssetDate(youngestAsset);

        if (oldestDate >= youngestDate)
        {
            return await GetAllAvailableAssets(ct);
        }

        await InitializePhotoClusters(oldestDate, youngestDate, ct);

        var assets = await TryGetAssetsFromClusterBasedRandomDates(ct);
        return assets ?? Enumerable.Empty<AssetResponseDto>();
    }

    private async Task<(AssetResponseDto? oldest, AssetResponseDto? youngest)> GetOldestAndYoungestAssetsAsync(CancellationToken ct)
    {
        try
        {
            var oldestResult = await _immichApi.SearchAssetsAsync(new MetadataSearchDto
            {
                Size = 1,
                Page = 1,
                Order = AssetOrder.Asc,
                Type = GetSearchAssetType(),
                WithExif = true,
                Visibility = _accountSettings.ShowArchived ? AssetVisibility.Archive : AssetVisibility.Timeline
            }, ct);

            var youngestResult = await _immichApi.SearchAssetsAsync(new MetadataSearchDto
            {
                Size = 1,
                Page = 1,
                Order = AssetOrder.Desc,
                Type = GetSearchAssetType(),
                WithExif = true,
                Visibility = _accountSettings.ShowArchived ? AssetVisibility.Archive : AssetVisibility.Timeline
            }, ct);

            return (oldestResult?.Assets?.Items?.FirstOrDefault(), youngestResult?.Assets?.Items?.FirstOrDefault());
        }
        catch
        {
            return (null, null);
        }
    }

    private static DateTime GetAssetDate(AssetResponseDto asset)
    {
        return asset.ExifInfo?.DateTimeOriginal?.DateTime ?? asset.FileCreatedAt.DateTime;
    }

    private async Task<List<AssetResponseDto>?> TryGetAssetsFromClusterBasedRandomDates(CancellationToken ct)
    {
        if (_photoClusters == null || _photoClusters.Count == 0)
        {
            return null;
        }

        var allAssets = new List<AssetResponseDto>();
        var requiredDateBlocks = Math.Ceiling((double)_requestedAssetCount / _assetsPerRandomDate);
        var maxDateAttempts = Math.Min((int)requiredDateBlocks * 4, Math.Max(12, _requestedAssetCount / 2));
        PhotoCluster? currentCluster = null;
        DateTime? currentRandomDate = null;

        for (var attempt = 0; attempt < maxDateAttempts && allAssets.Count < _requestedAssetCount; attempt++)
        {
            if (attempt % 4 == 0)
            {
                currentCluster = SelectRandomCluster();
                currentRandomDate = GenerateRandomDateFromCluster(currentCluster);
            }

            if (currentCluster == null || currentRandomDate == null)
            {
                continue;
            }

            var timeRangeAttempt = (attempt % 4) + 1;
            var (searchStartDate, searchEndDate) = GetSearchTimeRange(currentRandomDate.Value, timeRangeAttempt, currentCluster.IsSparse);
            var assets = await SearchAssetsInTimeRange(searchStartDate, searchEndDate, ct);

            if (assets.Count > 0)
            {
                allAssets.AddRange(assets);
            }
        }

        if (allAssets.Count >= Math.Min(_assetsPerRandomDate, _requestedAssetCount))
        {
            return allAssets;
        }

        return await GetAllAvailableAssets(ct);
    }

    private DateTime GenerateRandomDateFromCluster(PhotoCluster cluster)
    {
        var totalDays = (cluster.EndDate - cluster.StartDate).TotalDays;
        var randomDays = _random.NextDouble() * totalDays;
        return cluster.StartDate.AddDays(randomDays).Date;
    }

    private async Task<List<AssetResponseDto>> SearchAssetsInTimeRange(DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        try
        {
            var result = await _immichApi.SearchAssetsAsync(new MetadataSearchDto
            {
                TakenAfter = startDate,
                TakenBefore = endDate,
                Size = Math.Max(_assetsPerRandomDate * 4, 1000),
                Page = 1,
                Type = GetSearchAssetType(),
                WithExif = true,
                Visibility = _accountSettings.ShowArchived ? AssetVisibility.Archive : AssetVisibility.Timeline
            }, ct);

            var assets = result?.Assets?.Items?.ToList() ?? new List<AssetResponseDto>();
            var newAssets = assets.Where(asset => !_selectedAssetIds.Contains(asset.Id)).ToList();
            var selectedAssets = Shuffle(newAssets).Take(_assetsPerRandomDate).ToList();

            foreach (var asset in selectedAssets)
            {
                _selectedAssetIds.Add(asset.Id);
            }

            return selectedAssets;
        }
        catch
        {
            return new List<AssetResponseDto>();
        }
    }

    private (DateTime searchStart, DateTime searchEnd) GetSearchTimeRange(DateTime targetDate, int attemptNumber, bool isSparseCluster)
    {
        if (isSparseCluster)
        {
            return attemptNumber switch
            {
                1 => (targetDate.AddMonths(-6), targetDate.AddMonths(6)),
                2 => (targetDate.AddMonths(-12), targetDate.AddMonths(12)),
                3 => (targetDate.AddMonths(-18), targetDate.AddMonths(18)),
                _ => (targetDate.AddMonths(-24), targetDate.AddMonths(24))
            };
        }

        return attemptNumber switch
        {
            1 => (targetDate.AddDays(-7), targetDate.AddDays(7)),
            2 => (targetDate.AddMonths(-3), targetDate.AddMonths(3)),
            3 => (targetDate.AddMonths(-6), targetDate.AddMonths(6)),
            _ => (targetDate.AddMonths(-12), targetDate.AddMonths(12))
        };
    }

    private async Task<List<AssetResponseDto>> GetAllAvailableAssets(CancellationToken ct)
    {
        try
        {
            var result = await _immichApi.SearchAssetsAsync(new MetadataSearchDto
            {
                Size = Math.Max(1000, _requestedAssetCount * 6),
                Page = 1,
                Type = GetSearchAssetType(),
                WithExif = true,
                Visibility = _accountSettings.ShowArchived ? AssetVisibility.Archive : AssetVisibility.Timeline
            }, ct);

            var assets = result?.Assets?.Items?.ToList() ?? new List<AssetResponseDto>();
            var newAssets = assets.Where(asset => !_selectedAssetIds.Contains(asset.Id)).ToList();
            var shuffledAssets = Shuffle(newAssets);

            foreach (var asset in shuffledAssets.Take(_requestedAssetCount))
            {
                _selectedAssetIds.Add(asset.Id);
            }

            return shuffledAssets;
        }
        catch
        {
            return new List<AssetResponseDto>();
        }
    }

    private async Task InitializePhotoClusters(DateTime oldestDate, DateTime youngestDate, CancellationToken ct)
    {
        if (_clustersInitialized && _photoClusters != null)
        {
            return;
        }

        try
        {
            var boundedOldestDate = BoundDateRange(oldestDate, youngestDate);
            var rangeYears = (youngestDate - boundedOldestDate).TotalDays / 365.25;
            var shouldCache = _enableClusterCaching && rangeYears > 2;

            if (shouldCache)
            {
                var cacheKey = GenerateClusterCacheKey(boundedOldestDate, youngestDate);
                _photoClusters = await _apiCache.GetOrAddAsync(cacheKey, async () =>
                {
                    var monthlyStats = await GetMonthlyPhotoStatistics(boundedOldestDate, youngestDate, ct);
                    return CreateBalancedClusters(monthlyStats);
                });
            }
            else
            {
                var monthlyStats = await GetMonthlyPhotoStatistics(boundedOldestDate, youngestDate, ct);
                _photoClusters = CreateBalancedClusters(monthlyStats);
            }
        }
        catch
        {
            _photoClusters = new List<PhotoCluster>
            {
                new()
                {
                    StartDate = oldestDate,
                    EndDate = youngestDate,
                    PhotoCount = 1000,
                    Weight = 1.0
                }
            };
        }
        finally
        {
            _clustersInitialized = true;
        }
    }

    private async Task<List<(DateTime Month, int PhotoCount)>> GetMonthlyPhotoStatistics(DateTime oldestDate, DateTime youngestDate, CancellationToken ct)
    {
        var monthlyStats = new List<(DateTime Month, int PhotoCount)>();
        var currentDate = new DateTime(oldestDate.Year, oldestDate.Month, 1);
        var endDate = new DateTime(youngestDate.Year, youngestDate.Month, 1);

        while (currentDate <= endDate)
        {
            var monthStart = currentDate < oldestDate ? oldestDate : currentDate;
            var monthEnd = currentDate.AddMonths(1).AddDays(-1);
            if (monthEnd > youngestDate)
            {
                monthEnd = youngestDate;
            }

            try
            {
                var result = await _immichApi.SearchAssetsAsync(new MetadataSearchDto
                {
                    Size = 1,
                    Page = 1,
                    Type = GetSearchAssetType(),
                    TakenAfter = monthStart,
                    TakenBefore = monthEnd.AddDays(1),
                    WithExif = true,
                    Visibility = _accountSettings.ShowArchived ? AssetVisibility.Archive : AssetVisibility.Timeline
                }, ct);

                monthlyStats.Add((currentDate, result?.Assets?.Total ?? 0));
            }
            catch
            {
                monthlyStats.Add((currentDate, 0));
            }

            currentDate = currentDate.AddMonths(1);
        }

        return monthlyStats;
    }

    private List<PhotoCluster> CreateBalancedClusters(List<(DateTime Month, int PhotoCount)> monthlyStats)
    {
        var totalPhotos = monthlyStats.Sum(x => x.PhotoCount);
        if (totalPhotos == 0)
        {
            return new List<PhotoCluster>
            {
                new()
                {
                    StartDate = monthlyStats.First().Month,
                    EndDate = monthlyStats.Last().Month.AddMonths(1).AddDays(-1),
                    Weight = 1.0
                }
            };
        }

        var clusters = new List<PhotoCluster>();
        const int targetClusters = 10;
        var targetPhotosPerCluster = Math.Max(1, totalPhotos / targetClusters);

        var currentCluster = new PhotoCluster();
        var currentPhotoCount = 0;
        var firstMonth = true;

        for (var i = 0; i < monthlyStats.Count; i++)
        {
            var (month, photoCount) = monthlyStats[i];
            if (firstMonth)
            {
                currentCluster.StartDate = month;
                firstMonth = false;
            }

            currentPhotoCount += photoCount;
            currentCluster.EndDate = month.AddMonths(1).AddDays(-1);

            var isLastMonth = i == monthlyStats.Count - 1;
            if (currentPhotoCount >= targetPhotosPerCluster && !isLastMonth)
            {
                currentCluster.PhotoCount = currentPhotoCount;
                clusters.Add(currentCluster);
                currentCluster = new PhotoCluster { StartDate = month.AddMonths(1) };
                currentPhotoCount = 0;
            }
        }

        if (currentPhotoCount > 0)
        {
            currentCluster.PhotoCount = currentPhotoCount;
            clusters.Add(currentCluster);
        }

        foreach (var cluster in clusters)
        {
            cluster.Weight = 1.0 / clusters.Count;
            var clusterDurationMonths = Math.Max(1, (cluster.EndDate - cluster.StartDate).Days / 30.0);
            var photosPerMonth = cluster.PhotoCount / clusterDurationMonths;
            cluster.IsSparse = photosPerMonth < SparseClusterThreshold;
        }

        return clusters;
    }

    private PhotoCluster SelectRandomCluster()
    {
        if (_photoClusters == null || _photoClusters.Count == 0)
        {
            throw new InvalidOperationException("Clusters are not initialized.");
        }

        var randomValue = _random.NextDouble();
        var cumulativeWeight = 0.0;

        foreach (var cluster in _photoClusters)
        {
            cumulativeWeight += cluster.Weight;
            if (randomValue <= cumulativeWeight)
            {
                return cluster;
            }
        }

        return _photoClusters.Last();
    }

    private DateTime BoundDateRange(DateTime oldestDate, DateTime youngestDate)
    {
        var maxOldestDate = youngestDate.AddYears(-MaxClusterRangeYears);
        return oldestDate < maxOldestDate ? maxOldestDate : oldestDate;
    }

    private string GenerateClusterCacheKey(DateTime oldestDate, DateTime youngestDate)
    {
        return $"{ClusterCachePrefix}:v1:range={oldestDate:yyyy-MM}to{youngestDate:yyyy-MM}:archived={_accountSettings.ShowArchived}:videos={_accountSettings.ShowVideos}";
    }

    private AssetTypeEnum? GetSearchAssetType() => _accountSettings.ShowVideos ? null : AssetTypeEnum.IMAGE;

    private static List<AssetResponseDto> Shuffle(List<AssetResponseDto> assets)
    {
        for (var i = assets.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (assets[i], assets[j]) = (assets[j], assets[i]);
        }

        return assets;
    }
}
