using System.Net.Http;
using System.Reflection;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic;
using ImmichFrame.Core.Logic.Pool;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic;

[TestFixture]
public class PooledImmichFrameLogicTests
{
    [Test]
    public void BuildPool_WithoutSelectionFilters_UsesFixedChronologicalRandomizedPool()
    {
        var accountSettings = new Mock<IAccountSettings>();
        accountSettings.SetupGet(x => x.ApiKey).Returns("test-key");
        accountSettings.SetupGet(x => x.ImmichServerUrl).Returns("https://immich.example");
        accountSettings.SetupGet(x => x.ShowFavorites).Returns(false);
        accountSettings.SetupGet(x => x.ShowMemories).Returns(false);
        accountSettings.SetupGet(x => x.Albums).Returns([]);
        accountSettings.SetupGet(x => x.People).Returns([]);
        accountSettings.SetupGet(x => x.Tags).Returns([]);

        var generalSettings = new Mock<IGeneralSettings>();
        generalSettings.SetupGet(x => x.RefreshAlbumPeopleInterval).Returns(12);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient("ImmichApiAccountClient")).Returns(new HttpClient());

        var logic = new PooledImmichFrameLogic(accountSettings.Object, generalSettings.Object, httpClientFactory.Object);
        var pool = (IAssetPool)typeof(PooledImmichFrameLogic)
            .GetField("_pool", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(logic)!;

        Assert.That(pool, Is.TypeOf<ChronologicalAssetsPoolWrapper>());
    }
}
