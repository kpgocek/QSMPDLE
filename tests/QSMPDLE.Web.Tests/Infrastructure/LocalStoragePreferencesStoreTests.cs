using FluentAssertions;
using QSMPDLE.Web.Features.Personalization.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;

namespace QSMPDLE.Web.Tests.Infrastructure;

public sealed class LocalStoragePreferencesStoreTests
{
    private const string Key = "qsmpdle-preferences";

    [Fact]
    public async Task SaveAsync_Reduced_StoresTheJsonThePageScriptReads()
    {
        var browser = new FakeBrowser();
        var store = new LocalStoragePreferencesStore(browser.LocalStorage);

        await store.SaveAsync(new PlayerPreferences { Motion = MotionPreference.Reduced });

        // The script at the top of App.razor reads this before the app connects.
        browser.Items[Key].Should().Contain("\"motion\":\"Reduced\"");
        (await store.GetAsync()).Motion.Should().Be(MotionPreference.Reduced);
    }

    [Fact]
    public async Task SaveAsync_Theme_StoresTheIdThePageScriptSetsOnTheRoot()
    {
        var browser = new FakeBrowser();
        var store = new LocalStoragePreferencesStore(browser.LocalStorage);

        await store.SaveAsync(new PlayerPreferences { Theme = "navy" });

        browser.Items[Key].Should().Contain("\"theme\":\"navy\"");
    }

    [Fact]
    public async Task GetAsync_NothingSaved_FollowsTheSystem()
    {
        var store = new LocalStoragePreferencesStore(new FakeBrowser().LocalStorage);

        (await store.GetAsync()).Should().Be(new PlayerPreferences());
    }

    [Fact]
    public async Task GetAsync_UnreadableValue_FallsBackToDefaults()
    {
        var browser = new FakeBrowser();
        browser.Items[Key] = "{not json";
        var store = new LocalStoragePreferencesStore(browser.LocalStorage);

        (await store.GetAsync()).Should().Be(new PlayerPreferences());
    }
}
