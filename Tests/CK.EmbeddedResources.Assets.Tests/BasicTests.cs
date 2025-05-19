using CK.Core;
using NUnit.Framework;
using Shouldly;
using System;
using System.IO;
using static CK.Testing.MonitorTestHelper;

namespace CK.EmbeddedResources.Assets.Tests;

[TestFixture]
public class BasicTests
{
    [Test]
    public void TargetPath_can_be_empty()
    {
        Type type = typeof( T1.Package );
        // This is a Local project: we obtain a FileSystemResourceContainer.
        string localFavIconPath;
        {
            var c = type.CreateResourcesContainer( TestHelper.Monitor );
            c.IsValid.ShouldBeTrue();
            c.ShouldBeAssignableTo<FileSystemResourceContainer>();
            c.LoadAssets( TestHelper.Monitor, "some/path", out var assets, "assets" ).ShouldBeTrue();
            Throw.Assert( assets != null );
            assets.Assets.Count.ShouldBe( 1 );
            // assets.jsonc defines is { "targetPath": "" }
            // The defaultTargetPath "some/path" is ignored.
            assets.Assets["favicon.ico"].Override.ShouldBe( ResourceOverrideKind.None );
            // The ResourceName is the full path of the file.
            var sep = Path.DirectorySeparatorChar;
            localFavIconPath = assets.Assets["favicon.ico"].Origin.FullResourceName;
            localFavIconPath.ShouldBe( $"{c.ResourcePrefix}assets{sep}favicon.ico" );
            localFavIconPath.ShouldBeSameAs( assets.Assets["favicon.ico"].Origin.LocalFilePath );
        }
        // Same as above but from the embedded resources.
        {
            var c = type.Assembly.GetResources().CreateResourcesContainerForType( TestHelper.Monitor, type );
            c.IsValid.ShouldBeTrue();
            c.ShouldBeAssignableTo<AssemblyResourceContainer>();
            c.LoadAssets( TestHelper.Monitor, "some/path", out var assets, "assets" ).ShouldBeTrue();
            Throw.Assert( assets != null );
            assets.Assets.Count.ShouldBe( 1 );
            // assets.jsonc defines is { "targetPath": "" }
            // The defaultTargetPath "some/path" is ignored.
            assets.Assets["favicon.ico"].Override.ShouldBe( ResourceOverrideKind.None );
            var o = assets.Assets["favicon.ico"].Origin;
            // The LocalFilePath exists.
            o.LocalFilePath.ShouldBe( localFavIconPath );
            c.ResourcePrefix.ShouldBe( "ck@T1/Res/" );
            o.FullResourceName.ShouldBe( $"{c.ResourcePrefix}assets/favicon.ico" );
        }
    }

    [Test]
    public void Override_required_failure()
    {
        using( TestHelper.Monitor.CollectTexts( out var logs  ) )
        {
            var f = TryLoadCombine( TestHelper.Monitor, typeof( T1.Package ), typeof( T2.Package ) );
            logs.ShouldContain( """
                    Asset 'favicon.ico' in resources of 'CK.EmbeddedResources.Assets.Tests.T2.Package' type overides the existing asset from resources of 'CK.EmbeddedResources.Assets.Tests.T1.Package' type.
                    An explicit override declaration "O": [..., "favicon.ico", ...] is required.
                    """ );
        }
    }

    [Test]
    public void Override_required_success()
    {
        var f = TryLoadCombine( TestHelper.Monitor, typeof( T1.Package ), typeof( T3.Package ) );
        Throw.Assert( f != null );
        f.IsAmbiguous.ShouldBeFalse();
        f.Assets["favicon.ico"].Origin.Container.DisplayName.ShouldBe( "resources of 'CK.EmbeddedResources.Assets.Tests.T3.Package' type" );
    }

    static FinalResourceAssetSet? TryLoadCombine( IActivityMonitor monitor, Type head, params Type[] remainders )
    {
        head.Namespace.ShouldNotBeNull();
        var resources = typeof( BasicTests ).Assembly.GetResources();
        // Loads the head defintions and calls ToInitialFinalSet.
        var cHead = resources.CreateResourcesContainerForType( TestHelper.Monitor, head );
        if( !cHead.LoadAssets( monitor, head.Namespace.Replace( '.', '/' ), out var headDefinitionSet, "assets" ) )
        {
            return null;
        }
        headDefinitionSet.ShouldNotBeNull();
        var final = headDefinitionSet.ToInitialFinalSet( monitor ).ShouldNotBeNull();

        // 
        foreach( var type in remainders )
        {
            type.Namespace.ShouldNotBeNull();
            var c = resources.CreateResourcesContainerForType( TestHelper.Monitor, type );
            c.IsValid.ShouldBeTrue();
            if( !c.LoadAssets( monitor, type.Namespace.Replace( '.', '/' ), out var definitions, "assets" ) )
            {
                return null;
            }
            definitions.ShouldNotBeNull();
            final = definitions.Combine( monitor, final ).ShouldNotBeNull();
        }
        return final;
    }
}
