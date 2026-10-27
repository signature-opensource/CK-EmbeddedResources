using CK.Core;
using NUnit.Framework;
using Shouldly;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.EmbeddedResources.Assets.Tests;

/// <summary>
/// <see cref="FinalResourceAssetSet.Aggregate(FinalResourceAssetSet)"/> was not covered by any test.
/// <para>
/// Its class summary states: "By aggregating 2 final sets together. If the same target path associated
/// to different resource exists, the resulting set can be IsAmbiguous (it contains at least one non
/// null FinalResourceAsset.Ambiguities)."
/// </para>
/// <para>
/// It did not: the collision branch was <c>f1.AddAmbiguities( f2.Ambiguities )</c>, which carried over
/// f2's ambiguities but never recorded <c>f2.Origin</c> itself, so the second resource was dropped and
/// the result claimed to be unambiguous. It is now <c>f1.AddAmbiguity( f2 )</c>, mirroring
/// <c>FinalTranslationSet.AggregateTranslations</c> in CK.EmbeddedResources.Globalization.
/// </para>
/// </summary>
[TestFixture]
public class AggregateTests
{
    static FinalResourceAssetSet OneAsset( string packageName, string content )
    {
        var c = new CodeGenResourceContainer( packageName );
        c.AddText( "assets/logo.png", content );
        c.LoadAssets( TestHelper.Monitor, "", out var definitions, "assets" ).ShouldBeTrue();
        var final = definitions.ShouldNotBeNull().ToInitialFinalSet( TestHelper.Monitor ).ShouldNotBeNull();
        final.Assets.Count.ShouldBe( 1 );
        final.IsAmbiguous.ShouldBeFalse();
        return final;
    }

    [Test]
    public void Aggregate_of_a_colliding_target_path_is_ambiguous()
    {
        var f1 = OneAsset( "P1", "P1-content" );
        var f2 = OneAsset( "P2", "P2-content" );

        var agg = f1.Aggregate( f2 );

        agg.Assets.Count.ShouldBe( 1 );
        var a = agg.Assets["logo.png"];
        a.Ambiguities.ShouldNotBeNull()
                     .Select( x => x.Container.DisplayName )
                     .ShouldContain( "P2" );
        agg.IsAmbiguous.ShouldBeTrue();
    }

    /// <summary>
    /// Commutativity holds for what the caller acts on: the set of resources and the ambiguity status.
    /// It does not hold for which resource is nominally the <see cref="FinalResourceAsset.Origin"/> of a
    /// conflicting pair - the receiver of the call keeps its own - and that is not a problem: an ambiguous
    /// final set is rejected as a whole, so the choice never reaches an installer.
    /// The sibling <c>FinalTranslationSet.Aggregate</c> behaves identically.
    /// </summary>
    [Test]
    public void Aggregate_is_commutative_on_resources_and_on_ambiguity()
    {
        var f1 = OneAsset( "P1", "P1-content" );
        var f2 = OneAsset( "P2", "P2-content" );

        var left = f1.Aggregate( f2 );
        var right = f2.Aggregate( f1 );

        left.IsAmbiguous.ShouldBeTrue();
        right.IsAmbiguous.ShouldBe( left.IsAmbiguous );
        left.Assets.Keys.ShouldBe( right.Assets.Keys );

        // Both directions know about both resources, whichever one ends up being the Origin.
        static string[] All( FinalResourceAsset a )
            => a.Ambiguities!.Append( a.Origin ).Select( x => x.Container.DisplayName ).Order().ToArray();

        All( left.Assets["logo.png"] ).ShouldBe( ["P1", "P2"] );
        All( right.Assets["logo.png"] ).ShouldBe( ["P1", "P2"] );
    }

    [Test]
    public void Aggregate_of_the_same_set_twice_is_not_an_ambiguity()
    {
        var f1 = OneAsset( "P1", "P1-content" );

        var agg = f1.Aggregate( f1 );

        agg.IsAmbiguous.ShouldBeFalse();
        agg.Assets["logo.png"].Ambiguities.ShouldBeNull();
    }

    /// <summary>
    /// An ambiguity carried by an asset that only the smaller set has must be reported by
    /// <see cref="FinalResourceAssetSet.IsAmbiguous"/>, whatever the aggregation order.
    /// </summary>
    [Test]
    public void Aggregate_reports_an_ambiguity_carried_by_the_smaller_set()
    {
        // A one-asset set that is already ambiguous on 'logo.png'.
        var ambiguous = OneAsset( "P1", "P1-content" ).Aggregate( OneAsset( "P2", "P2-content" ) );
        ambiguous.IsAmbiguous.ShouldBeTrue();
        ambiguous.Assets.Count.ShouldBe( 1 );

        // A larger, unambiguous set that shares no target path with it.
        var other = new CodeGenResourceContainer( "P3" );
        other.AddText( "assets/a.png", "a" );
        other.AddText( "assets/b.png", "b" );
        other.LoadAssets( TestHelper.Monitor, "", out var def3, "assets" ).ShouldBeTrue();
        var bigger = def3.ShouldNotBeNull().ToInitialFinalSet( TestHelper.Monitor ).ShouldNotBeNull();
        bigger.Assets.Count.ShouldBe( 2 );

        foreach( var agg in new[] { ambiguous.Aggregate( bigger ), bigger.Aggregate( ambiguous ) } )
        {
            agg.Assets.Count.ShouldBe( 3 );
            agg.Assets["logo.png"].Ambiguities.ShouldNotBeNull();
            agg.IsAmbiguous.ShouldBeTrue();
        }
    }
}
