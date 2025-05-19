using CK.Core;
using Shouldly;
using NUnit.Framework;
using System;

namespace CK.EmbeddedResources.Tests;

[TestFixture]
public class ImmutableOrdinalSortedStringsTests
{
    [Test]
    public void GetPrefixedStrings_by_prefix_simple_test()
    {
        ImmutableOrdinalSortedStrings s = new( "a", "pA", "pA", "pB", "pC", "z" );
        ReadOnlyMemory<string> r = s.GetPrefixedStrings( "p" );
        r.Length.ShouldBe( 4 );
        r.ToArray().ShouldBe( ["pA", "pA", "pB", "pC"] );

        r = s.GetPrefixedStrings( "" );
        r.ToArray().ShouldBe( s.All );

        r = s.GetPrefixedStrings( "\u00FF" );
        r.Length.ShouldBe( 0 );

        r = s.GetPrefixedStrings( "a" );
        r.Length.ShouldBe( 1 );
        r.Span[0].ShouldBe( "a" );

        r = s.GetPrefixedStrings( "aa" );
        r.Length.ShouldBe( 0 );

        r = s.GetPrefixedStrings( "!" );
        r.Length.ShouldBe( 0 );

        r = s.GetPrefixedStrings( "zz" );
        r.Length.ShouldBe( 0 );

        r = s.GetPrefixedStrings( "pA" );
        r.Length.ShouldBe( 2 );
        r.Span[0].ShouldBe( "pA" );
        r.Span[1].ShouldBe( "pA" );
    }


    [Test]
    public void IsPrefix()
    {
        ImmutableOrdinalSortedStrings s = new( "zz", "zzz", "a", "pA", "pA", "pB", "pC" );
        s.IsPrefix( "" ).ShouldBeTrue();
        s.IsPrefix( "p" ).ShouldBeTrue();
        s.IsPrefix( "z" ).ShouldBeTrue();

        s.IsPrefix( "zz" ).ShouldBeFalse();
        s.IsPrefix( "a" ).ShouldBeFalse();
        s.IsPrefix( "aa" ).ShouldBeFalse();
        s.IsPrefix( "pX" ).ShouldBeFalse();
        s.IsPrefix( "b" ).ShouldBeFalse();
    }

    [Test]
    public void IsPrefix_is_false_if_the_string_exists()
    {
        ImmutableOrdinalSortedStrings s = new( "prefix1" );
        s.IsPrefix( "prefix" ).ShouldBeTrue();
        s.IsPrefix( "prefix1" ).ShouldBeFalse();

        s = new( "prefix1", "prefix2", "prefi" );
        s.IsPrefix( "prefix" ).ShouldBeTrue();
        s.IsPrefix( "prefix1" ).ShouldBeFalse();

    }

}
