using CommunityToolkit.HighPerformance;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;

namespace CK.Core;

/// <summary>
/// Captures an ordered array of strings.
/// Strings don't have to be unique.
/// </summary>
public readonly struct ImmutableOrdinalSortedStrings
{
    /// <summary>
    /// The whole array of strings.
    /// </summary>
    public readonly ImmutableArray<string> All;

    /// <summary>
    /// Initializes a new <see cref="ImmutableOrdinalSortedStrings"/> from any array.
    /// </summary>
    /// <param name="strings">The strings.</param>
    public ImmutableOrdinalSortedStrings( params string[] strings )
        : this( true, (string[])strings.Clone() )
    {
    }

    /// <summary>
    /// Initializes a new <see cref="ImmutableOrdinalSortedStrings"/> from a set of strings.
    /// </summary>
    /// <param name="strings">The strings.</param>
    public ImmutableOrdinalSortedStrings( IEnumerable<string> strings )
        : this( true, strings.ToArray() )
    {
    }

    /// <summary>
    /// Creates a <see cref="ImmutableOrdinalSortedStrings"/> on an existing array whose ownership
    /// is transfered to the result: the array must not be altered in any way.
    /// </summary>
    /// <param name="transferOwnership">Array to transfer.</param>
    /// <param name="mustSort">True to sort the array, false if the array is already sorted.</param>
    /// <returns>The strings.</returns>
    public static ImmutableOrdinalSortedStrings UnsafeCreate( string[] transferOwnership, bool mustSort )
    {
        return mustSort
                ? new ImmutableOrdinalSortedStrings( true, transferOwnership )
                : new ImmutableOrdinalSortedStrings( true, true, transferOwnership );
    }

    ImmutableOrdinalSortedStrings( bool ownsIt, string[] strings )
    {
        Array.Sort( strings, StringComparer.Ordinal );
        All = ImmutableCollectionsMarshal.AsImmutableArray( strings );
    }

    ImmutableOrdinalSortedStrings( bool ownsIt, bool sorted, string[] strings )
    {
        All = ImmutableCollectionsMarshal.AsImmutableArray( strings );
    }

    /// <summary>
    /// Gets whether <see cref="All"/> can be used or has not been initialized.
    /// </summary>
    public bool IsValid => !All.IsDefault;

    /// <summary>
    /// Gets the index of the string in <see cref="All"/> or a negative
    /// value if not found.
    /// </summary>
    /// <param name="name">Name to search.</param>
    /// <returns>
    /// A zero or positive index when found, or a negative number that is the bitwise complement
    /// of the potential insertion index if not found.
    /// </returns>
    public int IndexOf( string name )
    {
        Throw.CheckNotNullArgument( name );
        return IndexOf( name, All.AsSpan() );
    }

    readonly struct Finder : IComparable<string>
    {
        readonly string _prefix;

        public Finder( string prefix ) => _prefix = prefix;

        public int CompareTo( string? other )
        {
            Throw.DebugAssert( other != null );
            return _prefix.AsSpan().CompareTo( other.AsSpan(), StringComparison.Ordinal );
        }
    }

    internal static int IndexOf( string name, ReadOnlySpan<string> sAll ) => sAll.BinarySearch( new Finder( name ) );

    readonly struct BegFinder : IComparable<string>
    {
        readonly string _prefix;

        public BegFinder( string prefix ) => _prefix = prefix;

        public int CompareTo( string? other )
        {
            Throw.DebugAssert( other != null );
            int l = Math.Min( _prefix.Length, other.Length );
            int cmp = _prefix.AsSpan().CompareTo( other.AsSpan( 0, l ), StringComparison.Ordinal );
            return cmp == 0 ? -1 : cmp;
        }
    }

    /// <summary>
    /// Gets the index of the first string in <see cref="All"/> that starts with the <paramref name="prefix"/>.
    /// </summary>
    /// <param name="prefix">Prefix to search.</param>
    /// <returns>The resulting index or -1 if not found.</returns>
    public int GetPrefixedStart( string prefix ) => GetPrefixedStart( prefix, All.AsSpan() );

    internal static int GetPrefixedStart( string prefix, ReadOnlySpan<string> sAll )
    {
        int beg = sAll.BinarySearch( new BegFinder( prefix ) );
        if( beg < 0 )
        {
            beg = ~beg;
            if( beg == sAll.Length ) return -1;
        }
        return beg;
    }

    readonly struct EndFinder : IComparable<string>
    {
        readonly string _prefix;

        public EndFinder( string prefix ) => _prefix = prefix;

        public int CompareTo( string? other )
        {
            Throw.DebugAssert( other != null );
            int l = Math.Min( _prefix.Length, other.Length );
            int cmp = _prefix.AsSpan().CompareTo( other.AsSpan( 0, l ), StringComparison.Ordinal );
            return cmp == 0 ? 1 : cmp;
        }
    }

    /// <summary>
    /// Gets the range of strings in <see cref="All"/> that start with the <paramref name="prefix"/>.
    /// </summary>
    /// <param name="prefix">Common prefix to search.</param>
    /// <returns>The resulting range. Length (and Idx) is 0 if no strings can be found.</returns>
    public (int Idx, int Length) GetPrefixedRange( string prefix ) => GetPrefixedRange( prefix, All.AsSpan() );

    internal static (int Idx, int Length) GetPrefixedRange( string prefix, ReadOnlySpan<string> sAll )
    {
        int beg = sAll.BinarySearch( new BegFinder( prefix ) );
        if( beg < 0 )
        {
            beg = ~beg;
            if( beg == sAll.Length ) return default;
        }
        return (beg, GetEndIndex( prefix, sAll.Slice( beg ) ));
    }

    internal static int GetEndIndex( string prefix, ReadOnlySpan<string> tail )
    {
        int len = tail.BinarySearch( new EndFinder( prefix ) );
        if( len < 0 )
        {
            len = ~len;
        }
        return len;
    }


    /// <summary>
    /// Gets the range of strings in <see cref="All"/> that start with the <paramref name="prefix"/>.
    /// </summary>
    /// <param name="prefix">Common prefix to search.</param>
    /// <returns>The resulting strings.</returns>
    public ReadOnlyMemory<string> GetPrefixedStrings( string prefix ) => GetPrefixedStrings( prefix, All.AsMemory() );

    /// <summary>
    /// Gets the range of strings in <paramref name="tail"/> that start with the <paramref name="prefix"/>.
    /// </summary>
    /// <param name="prefix">Common prefix to search.</param>
    /// <param name="tail">The strings to consider.</param>
    /// <returns>The resulting strings.</returns>
    internal static ReadOnlyMemory<string> GetPrefixedStrings( string prefix, ReadOnlyMemory<string> tail )
    {
        var (i, l) = GetPrefixedRange( prefix, tail.Span );
        return tail.Slice( i, l );
    }

    /// <summary>
    /// Gets whether the <paramref name="prefix"/> exists: at least one string starts with it.
    /// </summary>
    /// <param name="prefix">Prefix to search.</param>
    /// <returns>True if found, false otherwise.</returns>
    public bool IsPrefix( string prefix ) => IsPrefix( prefix, All.AsSpan() );

    readonly struct PrefixPredicate : IComparable<string>
    {
        readonly string _prefix;

        public PrefixPredicate( string prefix ) => _prefix = prefix;

        public int CompareTo( string? other )
        {
            Throw.DebugAssert( other != null );
            int l = other.Length;
            bool isCandidate = _prefix.Length < l;
            if( isCandidate ) l = _prefix.Length;
            int cmp = _prefix.AsSpan().CompareTo( other.AsSpan( 0, l ), StringComparison.Ordinal );
            return cmp == 0
                    ? (isCandidate ? 0 : -1)
                    : cmp;
        }
    }

    internal static bool IsPrefix( string prefix, ReadOnlySpan<string> tail )
    {
        return tail.BinarySearch( new PrefixPredicate( prefix ) ) >= 0;
    }


}
