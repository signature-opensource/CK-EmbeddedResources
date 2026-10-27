using CK.Core;
using NUnit.Framework;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.EmbeddedResources.Assets.Tests;

/// <summary>
/// Two resources that map to the same final target path is an error that must be reported as such
/// (and not as an unexpected exception that stops the processing of the "assets.jsonc").
/// </summary>
[TestFixture]
public class MappingCollisionTests
{
    static (bool Success, ResourceAssetDefinitionSet? Assets, List<string> Logs) Load( string manifest, params string[] files )
    {
        var c = new CodeGenResourceContainer( "P" );
        foreach( var f in files ) c.AddText( "assets/" + f, f );
        c.AddText( "assets/assets.jsonc", manifest );
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            bool success = c.LoadAssets( TestHelper.Monitor, "", out var assets, "assets" );
            return (success, assets, logs.ToList());
        }
    }

    [Test]
    public void two_mappings_to_the_same_target_is_an_error()
    {
        var (success, _, logs) = Load( """{ "mappings": { "a": "x", "b": "x" } }""", "a/logo.png", "b/logo.png" );
        success.ShouldBeFalse();
        logs.ShouldContain( t => t.StartsWith( "Invalid mapping \"b\": \"x\"." ) && t.Contains( "maps to the same target 'x/logo.png'" ) );
        logs.ShouldNotContain( t => t.Contains( "While reading 'assets.jsonc'." ) );
    }

    [Test]
    public void a_mapping_to_the_target_of_an_unmapped_resource_is_an_error()
    {
        var (success, _, logs) = Load( """{ "mappings": { "a": "b" } }""", "a/logo.png", "b/logo.png" );
        success.ShouldBeFalse();
        logs.ShouldContain( t => t.Contains( "maps to 'b/logo.png'" ) && t.Contains( "already maps to the same target" ) );
        logs.ShouldNotContain( t => t.Contains( "While reading 'assets.jsonc'." ) );
    }

    [Test]
    public void a_mapping_whose_target_is_an_existing_asset_path_but_whose_assets_do_not_collide_is_valid()
    {
        var (success, assets, _) = Load( """{ "mappings": { "logo.png": "img", "f": "img/logo.png" } }""", "logo.png", "f/x.png" );
        success.ShouldBeTrue();
        assets.ShouldNotBeNull().Assets.Keys.Select( k => k.Path ).ShouldBe( ["img/logo.png", "img/logo.png/x.png"], ignoreOrder: true );
    }
}
