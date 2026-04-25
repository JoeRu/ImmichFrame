using System.Collections.Frozen;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Exceptions;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.AccountSelection;
using ImmichFrame.Core.Models;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic;

public class MultiImmichFrameLogicDelegate : IImmichFrameLogic
{
    private readonly FrozenDictionary<IAccountSettings, IAccountImmichFrameLogic> _accountToDelegate;
    private readonly IServerSettings _serverSettings;
    private readonly IAccountSelectionStrategy _accountSelectionStrategy;
    private readonly IAssetAccountTracker _tracker;
    private readonly ILogger<MultiImmichFrameLogicDelegate> _logger;

    public MultiImmichFrameLogicDelegate(IServerSettings serverSettings,
        Func<IAccountSettings, IAccountImmichFrameLogic> logicFactory, ILogger<MultiImmichFrameLogicDelegate> logger,
        IAccountSelectionStrategy accountSelectionStrategy, IAssetAccountTracker tracker)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accountSelectionStrategy = accountSelectionStrategy;
        _tracker = tracker;
        _serverSettings = serverSettings;
        _accountToDelegate = serverSettings.Accounts.ToFrozenDictionary(
            keySelector: a => a,
            elementSelector: logicFactory
        );
        _accountSelectionStrategy.Initialize(_accountToDelegate.Values);
    }

    public async Task<AssetResponseDto?> GetNextAsset() => (await _accountSelectionStrategy.GetNextAsset())?.ToAsset();


    public async Task<IEnumerable<AssetResponseDto>> GetAssets()
        => (await _accountSelectionStrategy.GetAssets()).Select(it => it.ToAsset());


    public Task<AssetResponseDto> GetAssetInfoById(Guid assetId)
        => GetWithFallback(
            assetId,
            async logic => (await logic.GetAssetInfoById(assetId)).WithAccount(logic),
            (logic, assetInfo) => Task.FromResult(assetInfo.WithAccount(logic)));


    public Task<IEnumerable<AlbumResponseDto>> GetAlbumInfoById(Guid assetId)
        => GetWithFallback(assetId, logic => logic.GetAlbumInfoById(assetId));


    public Task<AssetResponse> GetAsset(Guid assetId, AssetTypeEnum? assetType = null, string? rangeHeader = null)
        => GetWithFallback(assetId, logic => logic.GetAsset(assetId, assetType, rangeHeader));

    private async Task<T> GetWithFallback<T>(
        Guid assetId,
        Func<IAccountImmichFrameLogic, Task<T>> getValue,
        Func<IAccountImmichFrameLogic, AssetResponseDto, Task<T>>? resolveFromAssetInfo = null)
    {
        try
        {
            return await _accountSelectionStrategy.ForAsset(assetId, getValue);
        }
        catch (AssetNotFoundException)
        {
            foreach (var account in _accountToDelegate.Values)
            {
                AssetResponseDto assetInfo;
                try
                {
                    assetInfo = await account.GetAssetInfoById(assetId);
                }
                catch (AssetNotFoundException)
                {
                    continue;
                }

                if (assetInfo is null)
                {
                    continue;
                }

                var value = resolveFromAssetInfo is null
                    ? await getValue(account)
                    : await resolveFromAssetInfo(account, assetInfo);

                await TryRecordAssetLocation(account, assetId);
                return value;
            }

            throw;
        }
    }

    private async Task TryRecordAssetLocation(IAccountImmichFrameLogic account, Guid assetId)
    {
        try
        {
            await _tracker.RecordAssetLocation(account, assetId.ToString());
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to recache asset location for asset {assetId} on fallback account {account}", assetId, account.AccountSettings.ImmichServerUrl);
        }
    }

    public async Task<long> GetTotalAssets()
    {
        var allInts = await Task.WhenAll(_accountToDelegate.Values.Select(account => account.GetTotalAssets()));
        return allInts.Sum();
    }

    public Task SendWebhookNotification(IWebhookNotification notification) =>
        WebhookHelper.SendWebhookNotification(notification, _serverSettings.GeneralSettings.Webhook);
}

public static class AccountAndAssetExtensions
{
    public static AssetResponseDto ToAsset(this (IAccountImmichFrameLogic, AssetResponseDto) accountAndAsset)
    {
        var (account, asset) = accountAndAsset;
        return asset.WithAccount(account);
    }

    public static AssetResponseDto WithAccount(this AssetResponseDto asset, IAccountImmichFrameLogic account)
    {
        asset.ImmichServerUrl = account.AccountSettings.ImmichServerUrl;
        return asset;
    }
}
