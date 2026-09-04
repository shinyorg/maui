using Shiny;
using Shiny.Infrastructure;
using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// Behavioural tests for app link matching. This is the highest-risk part of the feature - a URL
/// arrives from outside the app and nothing upstream validates its shape.
/// </summary>
public class AppLinkRegistryTests
{
    class ProductViewModel
    {
        public int Id { get; set; }
        public string? Tab { get; set; }
    }

    static AppLinkRegistry Registry(params string[] templates)
    {
        var registry = new AppLinkRegistry();
        foreach (var template in templates)
        {
            registry.Add(new RegisteredAppLink(
                template,
                "Product",
                typeof(ProductViewModel),
                RegisterRoute: true,
                Apply: static (_, _) => true
            ));
        }
        return registry;
    }


    [Fact]
    public void LiteralTemplate_Matches()
    {
        var match = Registry("home").GetMatches(new Uri("myapp://home")).ShouldHaveSingleItem();
        match.Template.ShouldBe("home");
    }


    [Fact]
    public void CustomScheme_TreatsHostAsFirstPathSegment()
    {
        // "myapp://product/123" parses with Host "product" and AbsolutePath "/123" - the whole
        // reason path extraction cannot just use AbsolutePath.
        var match = Registry("product/{id}").GetMatches(new Uri("myapp://product/123")).ShouldHaveSingleItem();

        match.Values["id"].ShouldBe("123");
    }


    [Fact]
    public void HttpsScheme_TreatsHostAsDomainNotSegment()
    {
        var match = Registry("product/{id}").GetMatches(new Uri("https://shinylib.net/product/123")).ShouldHaveSingleItem();

        match.Values["id"].ShouldBe("123");
    }


    [Fact]
    public void SchemeRelativeUri_WithEmptyHost_StillMatches()
    {
        var match = Registry("product/{id}").GetMatches(new Uri("myapp:///product/123")).ShouldHaveSingleItem();

        match.Values["id"].ShouldBe("123");
    }


    [Fact]
    public void QueryValues_AreExtracted()
    {
        var match = Registry("product/{id}").GetMatches(new Uri("myapp://product/123?tab=reviews")).ShouldHaveSingleItem();

        match.Values["tab"].ShouldBe("reviews");
    }


    [Fact]
    public void ValueLookup_IsCaseInsensitive()
    {
        var match = Registry("product/{id}").GetMatches(new Uri("myapp://product/123?TAB=reviews")).ShouldHaveSingleItem();

        match.Values["Id"].ShouldBe("123");
        match.Values["tab"].ShouldBe("reviews");
    }


    [Fact]
    public void PathToken_WinsOverQueryValueOfTheSameName()
    {
        var match = Registry("product/{id}").GetMatches(new Uri("myapp://product/123?id=999")).ShouldHaveSingleItem();

        match.Values["id"].ShouldBe("123");
    }


    [Fact]
    public void LiteralSegments_AreMatchedCaseInsensitively()
    {
        Registry("product/{id}").GetMatches(new Uri("myapp://PRODUCT/123")).ShouldHaveSingleItem();
    }


    [Fact]
    public void DifferentSegmentCount_DoesNotMatch()
    {
        Registry("product/{id}").GetMatches(new Uri("myapp://product/123/extra")).ShouldBeEmpty();
        Registry("product/{id}").GetMatches(new Uri("myapp://product")).ShouldBeEmpty();
    }


    [Fact]
    public void UnknownPath_DoesNotMatch()
    {
        Registry("product/{id}").GetMatches(new Uri("myapp://order/123")).ShouldBeEmpty();
    }


    [Fact]
    public void LiteralTemplate_IsOrderedBeforeTokenTemplate()
    {
        // Registered token-first on purpose - ordering must come from specificity, not insertion.
        var matches = Registry("product/{id}", "product/featured")
            .GetMatches(new Uri("myapp://product/featured"))
            .ToList();

        matches.Count.ShouldBe(2);
        matches[0].Template.ShouldBe("product/featured");
        matches[1].Template.ShouldBe("product/{id}");
    }


    [Fact]
    public void EscapedPathSegment_IsDecoded()
    {
        var match = Registry("detail/{text}").GetMatches(new Uri("myapp://detail/hello%20world")).ShouldHaveSingleItem();

        match.Values["text"].ShouldBe("hello world");
    }


    [Fact]
    public void PlusInQueryValue_IsDecodedAsSpace()
    {
        var match = Registry("product/{id}").GetMatches(new Uri("myapp://product/1?tab=a+b")).ShouldHaveSingleItem();

        match.Values["tab"].ShouldBe("a b");
    }


    [Fact]
    public void QueryKeyWithoutValue_IsEmptyString()
    {
        var match = Registry("product/{id}").GetMatches(new Uri("myapp://product/1?flag")).ShouldHaveSingleItem();

        match.Values["flag"].ShouldBe(string.Empty);
    }


    [Fact]
    public void TrailingSlash_IsIgnored()
    {
        Registry("product/{id}").GetMatches(new Uri("myapp://product/123/")).ShouldHaveSingleItem();
    }


    [Fact]
    public void Fragment_IsNotPartOfTheMatch()
    {
        var match = Registry("product/{id}").GetMatches(new Uri("myapp://product/123#section")).ShouldHaveSingleItem();

        match.Values["id"].ShouldBe("123");
    }
}
