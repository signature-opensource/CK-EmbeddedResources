using Shouldly;
using Namespace.Does.Not.Matter;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.EmbeddedResources.Tests;

[TestFixture]
public class EmbeddedResourceTypeAttributeTests
{
    [Test]
    public void SomeType_has_a_Res_folder()
    {
        var resources = typeof( AssemblyResourcesTests ).Assembly.GetResources();

        var c = resources.CreateResourcesContainerForType( TestHelper.Monitor, typeof( SomeType ) );
        c.IsValid.ShouldBeTrue();

        // Reading content with the File Provider IFileInfo.
        var data = c.GetResource( "data.json" );
        using( var s = data.GetStream() )
        using( var r = new StreamReader( s ) )
        {
            r.ReadToEnd().Trim().ShouldBe( """{"Hello":"World"}""" );
        }

        // Reading content from a ResourceFolder.
        var locator2 = c.GetFolder( "" ).Resources.Single( r => r.ResourceName.SequenceEqual( "data.json" ) );
        locator2.ShouldBe( data );

        using( var s = locator2.GetStream() )
        using( var r = new StreamReader( s ) )
        {
            r.ReadToEnd().Trim().ShouldBe( """{"Hello":"World"}""" );
        }

    }
}
