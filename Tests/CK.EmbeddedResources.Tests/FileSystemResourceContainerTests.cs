using Shouldly;
using NUnit.Framework;
using System.IO;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.EmbeddedResources.Tests;

[TestFixture]
public class FileSystemResourceContainerTests
{
    [Test]
    public void FileSystemResourceContainer_simple_tests()
    {
        var c = new FileSystemResourceContainer( TestHelper.TestProjectFolder, "This test" );

        // FileSystemResourceContainer works with the platform separator.
        var normalizedPrefix = TestHelper.TestProjectFolder.Path.Replace( Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar ) + Path.DirectorySeparatorChar;

        c.ResourcePrefix.ShouldBe( normalizedPrefix );

        var content = c.GetFolder( "SomeType" );
        content.IsValid.ShouldBeTrue();
        content.FullFolderName.ShouldBe( normalizedPrefix + "SomeType" + Path.DirectorySeparatorChar );
        content.AllResources.Count().ShouldBe( 2 );
        var theOne = content.Resources.Single();
        theOne.FullResourceName.ShouldBe( $"{c.ResourcePrefix}SomeType{Path.DirectorySeparatorChar}SomeType.cs" );

        c.GetFolder( "C1/Res/" ).IsValid.ShouldBeTrue();
        c.GetFolder( "C1\\Res" ).IsValid.ShouldBeTrue();
        c.GetResource( "AssemblyResourcesTests.cs" ).IsValid.ShouldBeTrue();

        c.TryGetResource( "SomeType/SomeType.cs", out var locator ).ShouldBeTrue();

        var fsPath = Path.GetFullPath( TestHelper.TestProjectFolder.AppendPart( "SomeType" ).AppendPart( "SomeType.cs" ) );
        locator.FullResourceName.ShouldBe( fsPath, "FileSystemContainer uses the environment Path.DirectorySeparatorChar." );
    }

    [Test]
    public void FileSystemResourceContainer_AllResources()
    {
        var c = new FileSystemResourceContainer( TestHelper.TestProjectFolder, "This test" );
        c.AllResources.ShouldContain( new ResourceLocator( c, c.ResourcePrefix + "FileSystemResourceContainerTests.cs" ) );
        c.AllResources.ShouldContain( new ResourceLocator( c, c.ResourcePrefix + $"C1{Path.DirectorySeparatorChar}Res{Path.DirectorySeparatorChar}data.json" ) );
    }

}
