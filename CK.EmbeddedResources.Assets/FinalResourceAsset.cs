using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.EmbeddedResources;

/// <summary>
/// A final resource asset is a resource and a potential set of ambiguites.
/// </summary>
/// <param name="Origin">The first resource for the target path.</param>
/// <param name="Ambiguities">The resources that share the same target path if any.</param>
public readonly record struct FinalResourceAsset( ResourceLocator Origin, IEnumerable<ResourceLocator>? Ambiguities = null )
{
    /// <summary>
    /// Returns a resource asset with a new <see cref="Ambiguities"/>.
    /// </summary>
    /// <param name="locator">The locator that share the same target path as this <see cref="Origin"/>.</param>
    /// <returns>A new resource asset or this one if <paramref name="locator"/> is already known.</returns>
    public FinalResourceAsset AddAmbiguity( ResourceLocator locator )
    {
        if( locator == Origin
            || (Ambiguities != null && Ambiguities.Contains( locator )) )
        {
            return this;
        }
        return Ambiguities == null
            ? new FinalResourceAsset( Origin, [locator] )
            : new FinalResourceAsset( Origin, Ambiguities.Append( locator ) );
    }

    /// <summary>
    /// Returns a resource asset that combines this one with <paramref name="other"/> that targets the same
    /// path: the other's <see cref="Origin"/> and its own <see cref="Ambiguities"/> become ambiguities of
    /// this one.
    /// <para>
    /// When both share the same <see cref="Origin"/> (the same resource reached through two routes), this
    /// is returned unchanged: this is not an ambiguity.
    /// </para>
    /// </summary>
    /// <param name="other">The other asset for the same target path.</param>
    /// <returns>A new resource asset or this one if nothing new is known.</returns>
    public FinalResourceAsset AddAmbiguity( FinalResourceAsset other )
    {
        return AddAmbiguity( other.Origin ).AddAmbiguities( other.Ambiguities );
    }

    /// <summary>
    /// Adds multiple ambiguities at once.
    /// </summary>
    /// <param name="ambiguities">The ambiguities to add.</param>
    /// <returns>A new resource asset or this one if all <paramref name="ambiguities"/> are already known.</returns>
    public FinalResourceAsset AddAmbiguities( IEnumerable<ResourceLocator>? ambiguities )
    {
        if( ambiguities == null ) return this;
        FinalResourceAsset f = this;
        foreach( var a in ambiguities )
        {
            f = f.AddAmbiguity( a );
        }
        return f;
    }
}
