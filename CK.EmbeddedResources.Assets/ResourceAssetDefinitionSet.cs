using CK.Core;
using System.Collections.Generic;
using System.Linq;

namespace CK.EmbeddedResources;

/// <summary>
/// Captures a folder like structure of <see cref="ResourceAssetDefinition"/>: a read only dictionary
/// of target <see cref="NormalizedPath"/> to their <see cref="ResourceAssetDefinition"/>.
/// </summary>
public sealed class ResourceAssetDefinitionSet
{
    readonly Dictionary<NormalizedPath,ResourceAssetDefinition> _assets;

    /// <summary>
    /// Initializes a new set.
    /// </summary>
    /// <param name="assets">The assets.</param>
    public ResourceAssetDefinitionSet( Dictionary<NormalizedPath, ResourceAssetDefinition> assets )
    {
        _assets = assets;
    }

    /// <summary>
    /// Gets the asset definitions indexed by their target path.
    /// </summary>
    public IReadOnlyDictionary<NormalizedPath, ResourceAssetDefinition> Assets => _assets;

    /// <summary>
    /// Creates a new initial <see cref="FinalResourceAssetSet"/> from this one that is independent
    /// of any other asset definitions.
    /// <para>
    /// No <see cref="ResourceAssetDefinition.Override"/> in <see cref="Assets"/> cannot be <see cref="ResourceOverrideKind.Regular"/>
    /// otherwise it is an error and null is returned.
    /// </para>
    /// <para>
    /// <see cref="ResourceOverrideKind.Optional"/> are silently ignored: only <see cref="ResourceOverrideKind.None"/>
    /// and <see cref="ResourceOverrideKind.Always"/> are kept in the resulting set.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>The final set on success, false on error.</returns>
    public FinalResourceAssetSet? ToInitialFinalSet( IActivityMonitor monitor )
    {
        var buggyOverrides = _assets.Values.Where( a => a.Override is ResourceOverrideKind.Regular );
        if( buggyOverrides.Any() )
        {
            monitor.Error( $"""
                Invalid final set of resources. No asset can be defined as an override, the following resources are override definitions:
                {buggyOverrides.Select( o => o.ToString() ).Concatenate()}
                """ );
            return null;
        }
        var result = new Dictionary<NormalizedPath, FinalResourceAsset>( _assets.Count );
        foreach( var (path,definition) in _assets )
        {
            // Regular has trigerred the error above. Here we ignore Optional. 
            if( definition.Override is ResourceOverrideKind.None or ResourceOverrideKind.Always )
            {
                result.Add( path, new FinalResourceAsset( definition.Origin ) );
            }
        }
        // There's no ambiguity by design.
        return new FinalResourceAssetSet( result, false );
    }

    /// <summary>
    /// Apply this set of definitions to a base <see cref="FinalResourceAsset"/> to produce a new
    /// final set. This can create ambiguities as well as removing ones.
    /// <para>
    /// This operation is not idempotent. When applied twice on a set, false ambiguities
    /// will be created.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="baseSet">The base set to consider.</param>
    /// <returns>A final set or null on error.</returns>
    public FinalResourceAssetSet? Combine( IActivityMonitor monitor, FinalResourceAssetSet baseSet )
    {
        if( _assets.Count == 0 ) return baseSet;
        if( baseSet.Assets.Count == 0 ) return ToInitialFinalSet( monitor );

        // Avoids reallocation/doubling the size.
        var result = new Dictionary<NormalizedPath, FinalResourceAsset>( baseSet.Assets.Count + _assets.Count );
        bool success = true;
        bool isAmbiguous = false;
        // To be able to compute the IsAmbiguous, we should iterate on the baseSet
        // but we must also detect buggy overrides, so we iterate on the definitions.
        foreach( var (path,def) in _assets )
        {
            if( baseSet.Assets.TryGetValue( path, out var exists ) )
            {
                if( def.Override == ResourceOverrideKind.None )
                {
                    monitor.Warn( $"""
                        Asset '{path}' in {def.Origin.Container} overides the existing asset from {exists.Origin.Container}.
                        An explicit override declaration "O": [..., "{path}", ...] is required.
                        """ );
                        
                    var a = exists.AddAmbiguity( def.Origin );
                    result.Add( path, a );
                    isAmbiguous = true;
                }
                else
                {
                    monitor.Debug( $"Asset '{path}': {def} overrides {exists}." );
                    result.Add( path, new FinalResourceAsset( def.Origin ) );
                }
            }
            else
            {
                // The target path doesn't exist.
                if( def.Override == ResourceOverrideKind.Regular )
                {
                    monitor.Error( $"Asset '{path}' cannot be overridden by {def.Origin} as it doesn't already exist." );
                    success = false;
                }
                else if( def.Override == ResourceOverrideKind.Optional )
                {
                    monitor.Debug( $"Asset '{path}' optional override {def.Origin} skipped as it doesn't already exist." );
                }
                else
                {
                    Throw.DebugAssert( def.Override is ResourceOverrideKind.None or ResourceOverrideKind.Always );
                    result.Add( path, new FinalResourceAsset( def.Origin ) );
                }
            }
        }

        // On error (invalid regular override), give up.
        if( !success )
        {
            return null;
        }
        // Fill with the base set and compute the isAmbiguous.
        foreach( var (path,res) in baseSet.Assets )
        {
            // Skips already handled paths.
            if( _assets.ContainsKey( path ) ) continue;
            result.Add( path, res );
            isAmbiguous |= res.Ambiguities != null;
        }
        return new FinalResourceAssetSet( result, isAmbiguous );
    }

    /// <summary>
    /// Updates this assets the content of <paramref name="above"/>.
    /// If this set contains overrides, they apply and if it tries to redefine an existing
    /// resource, this is an error.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="above">The new resources to combine.</param>
    /// <param name="isPartialSet">
    /// When true, a regular override that overrides nothing is kept as it may apply to the eventual set.
    /// When false, a regular override that overrides nothing is discarded and a warning is emitted.
    /// </param>
    /// <returns>True on success, false on error.</returns>
    internal bool Combine( IActivityMonitor monitor, ResourceAssetDefinitionSet above, bool isPartialSet )
    {
        bool success = true;
        foreach( var (path, asset) in above.Assets )
        {
            if( _assets.TryGetValue( path, out var ourAsset ) )
            {
                if( asset.Override is ResourceOverrideKind.None )
                {
                    monitor.Error( $"""
                                    Asset '{path}' in {asset.Origin.Container} overides the existing asset from {ourAsset.Origin.Container}.
                                    An explicit override declaration "O": [... "{path}" ...] is required.
                                    """ );
                    success = false;
                }
                else
                {
                    // Whether it is a "O", "?O" or "!O" we don't care here as we override.
                    _assets[path] = asset;
                }
            }
            else
            {
                // No existing asset.
                if( isPartialSet
                    || asset.Override is ResourceOverrideKind.None or ResourceOverrideKind.Always )
                {
                    _assets.Add( path, asset );
                }
                else
                {
                    Throw.DebugAssert( asset.Override is ResourceOverrideKind.Regular or ResourceOverrideKind.Optional );
                    if( asset.Override is ResourceOverrideKind.Regular )
                    {
                        monitor.Warn( $"Invalid override \"O\" for {asset.Origin}: the target asset doesn't exist, there's nothing to override." );
                    }
                    // ResourceOverrideKind.Optional doesn't add the resource and doesn't warn
                    // since it doesn't already exist. 
                }
            }
        }
        return success;
    }
}
