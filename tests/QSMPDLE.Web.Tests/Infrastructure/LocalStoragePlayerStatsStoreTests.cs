using FluentAssertions;
using Microsoft.JSInterop;
using QSMPDLE.Web.Infrastructure.LocalStorage;

namespace QSMPDLE.Web.Tests.Infrastructure;

public sealed class LocalStoragePlayerStatsStoreTests
{
    private readonly FakeBrowser browser = new();

    [Fact]
    public async Task GetPlayerIdAsync_TwoComponentsOfANewVisitor_ShareOneStoredIdentity()
    {
        // An archive page asks from the game and from its calendar at once.
        var store = CreateStore();

        var ids = await Task.WhenAll(store.GetPlayerIdAsync(), store.GetPlayerIdAsync());

        ids[0].Should().NotBeEmpty();
        ids[1].Should().Be(ids[0]);
        (await CreateStore().GetPlayerIdAsync()).Should().Be(ids[0], "later visits read the identity the games were recorded under");
    }

    [Fact]
    public async Task GetPlayerIdAsync_AfterAFailedRead_StillWorks()
    {
        var store = CreateStore();
        browser.FailuresLeft = 1;
        await FluentActions.Awaiting(store.GetPlayerIdAsync).Should().ThrowAsync<JSException>();

        (await store.GetPlayerIdAsync().WaitAsync(TimeSpan.FromSeconds(5))).Should().NotBeEmpty();
    }

    private LocalStoragePlayerStatsStore CreateStore() => new(browser.LocalStorage);
}
