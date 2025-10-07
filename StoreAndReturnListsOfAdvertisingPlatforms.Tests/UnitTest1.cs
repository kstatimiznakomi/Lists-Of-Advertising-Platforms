using StoreAndReturnListsOfAdvertisingPlatforms.Service;

namespace StoreAndReturnListsOfAdvertisingPlatforms.StoreAndReturnListsOfAdvertisingPlatforms.Tests;

public class LocationAdvertiserServiceTests{
    [Theory]
    [InlineData("ru/svrd/revda", "/ru/svrd/revda")]
    [InlineData("/ru/svrd/revda", "/ru/svrd/revda")]
    [InlineData("/ru/svrd/revda/", "/ru/svrd/revda")]
    [InlineData("  /ru/svrd/revda  ", "/ru/svrd/revda")]
    [InlineData("  ru/svrd/revda  ", "/ru/svrd/revda")]
    [InlineData("  ru / svrd /   revda  ", "/ru/svrd/revda")]
    [InlineData("  /ru / svrd /   revda/  ", "/ru/svrd/revda")]
    [InlineData("  /ru / svrd    revda /  ", "/ru/svrdrevda")]
    [InlineData("ru", "/ru")]
    [InlineData(" ru ", "/ru")]
    public void NormalizeLocation_NormalizesVariousForms(string input, string expected){
        // arrange
        var svc = new LocationAdvertiserService();

        // act
        var actual = svc.NormalizeLocation(input);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizeLocation_ReturnsNull_ForNullOrWhiteSpace(){
        var svc = new LocationAdvertiserService();

        Assert.Null(svc.NormalizeLocation(null));
        Assert.Null(svc.NormalizeLocation(""));
        Assert.Null(svc.NormalizeLocation("   "));
        Assert.Null(svc.NormalizeLocation("\t\n"));
    }

    [Fact]
    public void LoadFromText_ParsesMultipleAdvertisers_AndSearchReturnsExpected(){
        var svc = new LocationAdvertiserService();

        // root advertiser ("/"), advertiser on /ru/svrd, and one deeper on /ru/svrd/revda
        var text = @"
    RootAdv: /
    AdvB: ru/svrd
    AdvA: ru/svrd/revda
    ";
        svc.LoadFromText(text);

        var resRevda = svc.Search("/ru/svrd/revda").ToArray();
        var resSvrd = svc.Search("ru/svrd").ToArray();
        var resRoot = svc.Search("/").ToArray();

        // revda should include A, B and Root
        Assert.Contains("AdvA", resRevda, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("AdvB", resRevda, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("RootAdv", resRevda, StringComparer.OrdinalIgnoreCase);

        // svrd should include B and Root, but not A
        Assert.Contains("AdvB", resSvrd, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("RootAdv", resSvrd, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("AdvA", resSvrd, StringComparer.OrdinalIgnoreCase);

        // root search returns root-level advertisers only
        Assert.Contains("RootAdv", resRoot, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("AdvA", resRoot, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("AdvB", resRoot, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadFromText_ParsesCommaSeparatedLocations_And_IgnoresInvalidLines(){
        var svc = new LocationAdvertiserService();

        var text = @"
    AdvX: ru/svrd/one, ru/svrd/two
    InvalidLineWithoutColon
    : noAdvertiser
    AdvY: /
    ";
        svc.LoadFromText(text);

        var one = svc.Search("/ru/svrd/one").ToArray();
        var two = svc.Search("/ru/svrd/two").ToArray();
        var root = svc.Search("/").ToArray();

        Assert.Contains("AdvX", one, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("AdvX", two, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("AdvY", root, StringComparer.OrdinalIgnoreCase);

        // invalid lines should not create advertisers
        Assert.Empty(svc.Search("/noAdvertiser"));
    }

    [Fact]
    public void LoadFromText_ReplacesTree_Atomically(){
        var svc = new LocationAdvertiserService();

        // initial load
        svc.LoadFromText("A: ru/svrd/one");
        Assert.Contains("A", svc.Search("/ru/svrd/one"), StringComparer.OrdinalIgnoreCase);

        // replace tree with different content (A should disappear)
        svc.LoadFromText("B: ru/svrd/two");
        var afterOld = svc.Search("/ru/svrd/one").ToArray();
        var afterNew = svc.Search("/ru/svrd/two").ToArray();

        Assert.Empty(afterOld); // old advertiser A gone
        Assert.Contains("B", afterNew, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Search_ReturnsEmpty_ForEmptyOrUnknownLocations(){
        var svc = new LocationAdvertiserService();

        // nothing loaded yet
        Assert.Empty(svc.Search(null));
        Assert.Empty(svc.Search(""));
        Assert.Empty(svc.Search("   "));

        // unknown path returns empty collection
        svc.LoadFromText("A: ru/svrd/one");
        Assert.Empty(svc.Search("/unknown/path"));
    }
}