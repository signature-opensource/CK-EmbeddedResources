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
