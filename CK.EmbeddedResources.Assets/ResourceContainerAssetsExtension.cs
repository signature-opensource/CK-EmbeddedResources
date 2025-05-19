using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CK.EmbeddedResources;

/// <summary>
/// <summary>
/// Extends <see cref="IResourceContainer"/> with LoadAssets methods.
/// </summary>
/// </summary>
public static class ResourceContainerAssetsExtension
{
    const string _manifestFileName = "assets.jsonc";

    /// <summary>
    /// Processes the <paramref name="folder"/> if it exists and returns a <see cref="ResourceAssetDefinitionSet"/>.
    /// Returns false on error (error has been logged).
    /// <para>
    /// The following folder:
    /// <code>
    /// assets/
    ///   logo.png
    ///   some-data/
    ///     data1.json
    ///     data2.jsonc
    ///   other-data/
    ///     data1.json
    /// </code>
    /// Will be reproduced in the <paramref name="defaultTargetPath"/> that is typically defined by the logical package
    /// that holds the resources.
    /// </para>
    /// <para>
    /// The optional file "assets.jsonc" can define path mappings for files or folders and declare
    /// the resources override behavior (all properties are optonal):
    /// <code>
    /// {
    ///     // Defines the default mapping for resources that have no defined mapping.
    ///     // When defined this replaces the provided <paramref name="defaultTargetPath"/> (that is
    ///     // typically the path from the component that holds the resources).
    ///     // It can be the empty string to target the root of the final asset folder.
    ///     "targetPath": "my/component/target",
    ///
    ///     // Optional mappings from locally define resources to a target path in the final asset folder.
    ///     "mappings": {
    ///         // The logo.png will be in the final "/logos/" asset folder.
    ///         "logo.png": "logos",
    ///         // The 2 data files will be in the final "/data/core/" asset folder.
    ///         "some-data/data1.json": "data/core"
    ///         "some-data/data2.json": "data/core",
    ///     }
    ///     
    ///     // Regular override: logo.png overrides an existing resource. The /logos/logo.png must already
    ///     //                   exist or a warning will be emitted.
    ///     "O": [ "logos/logo.png" ]
    ///     
    ///     // Optional override: data/core/data1.json will be updated only if it already exists.
    ///     //                    If the resource doesn't exits, nothing is done (and no warning is emitted).
    ///     "?O": [ "data/core/data1.json" ]
    ///
    ///     // Always override: the /other-data/data1.json will always be updated whether it exists or not.
    ///     //                  This is a risky behavior.
    ///     "!O": [ "other-data/data1.json" ]
    /// }
    /// </code>
    /// Mappings can map folders (<c>"some-data": "data/core"</c>). Mapping more than once a resource (either explicitly or
    /// through a folder) is an error.
    /// </para>
    /// <para>
    /// Override declarations are processed after the mappings. A resource can appear in at most one of the override
    /// section.
    /// </para>
    /// </summary>
    /// <param name="container">This container of resources.</param>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="defaultTargetPath">The default target path: a relative path in the final target root folder.</param>
    /// <param name="assets">Resulting definitions. Null on error or if the assets folder is not valid.</param>
    /// <param name="folder">The folder to load.</param>
    /// <returns>True on success, false on error.</returns>
    public static bool LoadAssets( this IResourceContainer container,
                                   IActivityMonitor monitor,
                                   NormalizedPath defaultTargetPath,
                                   out ResourceAssetDefinitionSet? assets,
                                   string folder = "assets" )
    {
        return DoLoad( monitor, container.GetFolder( folder ), defaultTargetPath, out assets );
    }

    static bool DoLoad( IActivityMonitor monitor,
                        ResourceFolder resources,
                        NormalizedPath defaultTargetPath,
                        out ResourceAssetDefinitionSet? assets )
    {
        assets = null;
        if( !resources.IsValid )
        {
            return true;
        }
        bool success = true;
        var final = new Dictionary<NormalizedPath, ResourceAssetDefinition>();
        ResourceLocator manifestFile = resources.GetResource( _manifestFileName );
        if( manifestFile.IsValid )
        {
            // When a "assets.jsonc" is here, its processing handle all
            // the resources: final is computed.
            success &= ReadManifest( monitor,
                                     resources,
                                     manifestFile,
                                     defaultTargetPath,
                                     final );
        }
        else
        {
            // No manifest, no mappings, we use the provided defaultTargetPath.
            foreach( var r in resources.AllResources )
            {
                if( r != manifestFile )
                {
                    var target = defaultTargetPath.Combine( r.FullResourceName.Substring( resources.FullFolderName.Length ) );
                    final.Add( target, new ResourceAssetDefinition( r, ResourceOverrideKind.None ) );
                }
            }
        }
        if( success )
        {
            assets = new ResourceAssetDefinitionSet( final );
        }
        return success;
    }

    static bool ReadManifest( IActivityMonitor monitor,
                              ResourceFolder resources,
                              ResourceLocator manifestFile,
                              NormalizedPath defaultTargetPath,
                              Dictionary<NormalizedPath, ResourceAssetDefinition> final )
    {
        bool success = true;
        try
        {
            using( var s = manifestFile.GetStream() )
            using( var manifest = JsonDocument.Parse( s, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            } ) )
            {
                var root = manifest.RootElement;
                // Read the overrides as a Dictionary<string,ResourceOverrideKind>. The path is the final
                // mapped path of the resource. It is easier to reason about overrides with final mapped names
                // rather than source resource paths.
                Dictionary<NormalizedPath, ResourceOverrideKind>? overrides = null;
                if( root.TryGetProperty( "O"u8, out var jsonOverride ) )
                {
                    success &= HandleOverrides( monitor, "O", ResourceOverrideKind.Regular, jsonOverride, ref overrides );
                }
                if( root.TryGetProperty( "?O"u8, out jsonOverride ) )
                {
                    success &= HandleOverrides( monitor, "?O", ResourceOverrideKind.Optional, jsonOverride, ref overrides );
                }
                if( root.TryGetProperty( "!O"u8, out jsonOverride ) )
                {
                    success &= HandleOverrides( monitor, "!O", ResourceOverrideKind.Always, jsonOverride, ref overrides );
                }

                // Handles the mappings: 
                // - Gets the target path. If this fails, we continue even if this could lead to
                //   a lot of cascading errors.
                if( root.TryGetProperty( "targetPath"u8, out var jsonTargetPath ) )
                {
                    if( !ReadPath( monitor, jsonTargetPath, out var targetPath ) )
                    {
                        success = false;
                    }
                    else
                    {
                        if( defaultTargetPath != targetPath )
                        {
                            monitor.Trace( $"Target path is '{targetPath}' instead of default '{defaultTargetPath}'." );
                            defaultTargetPath = targetPath;
                        }
                    }
                }
                // - Handles the mappings defined if any.
                if( root.TryGetProperty( "mappings"u8, out var jsonMappings ) )
                {
                    success &= HandleMappings( monitor, jsonMappings, resources, defaultTargetPath, ref overrides, final );
                }
                // - Complete the final with the unmapped resources.
                var remaining = new HashSet<ResourceLocator>( resources.AllResources.Where( r => r != manifestFile ) );
                foreach( var asset in final.Values )
                {
                    remaining.Remove( asset.Origin );
                }
                foreach( var r in remaining )
                {
                    success &= AddFinal( monitor, null, defaultTargetPath, resources, r, ref overrides, final );
                }
                // Now process all properties to detect unknown ones.
                var unknown = root.EnumerateObject().Where( p => !p.NameEquals( "targetPath"u8 )
                                                                    && !p.NameEquals( "mappings"u8 )
                                                                    && !p.NameEquals( "O"u8 )
                                                                    && !p.NameEquals( "?O"u8 )
                                                                    && !p.NameEquals( "!O"u8 ) )
                                                    .Select( p => p.Name )
                                                    .ToArray();
                if( unknown.Length != 0 )
                {
                    monitor.Error( $"""
                                Unexpected property: "{unknown.Concatenate("\", \"")}".
                                Expected only "targetPath", "mappings", "O", "?O" or "!O".
                                """ );
                    success = false;
                }
            }
            return success;
        }
        catch( Exception ex )
        {
            monitor.Error( $"While reading 'assets.jsonc'.", ex );
            success = false;
        }
        return success;

        static bool HandleMappings( IActivityMonitor monitor,
                                    JsonElement maps,
                                    ResourceFolder resources,
                                    NormalizedPath defaultTargetPath,
                                    ref Dictionary<NormalizedPath, ResourceOverrideKind>? overrides,
                                    Dictionary<NormalizedPath, ResourceAssetDefinition> final )
        {
            if( maps.ValueKind is not JsonValueKind.Object )
            {
                monitor.Error( "Invalid  \"mappings\": it must be an object that maps resource names to target folders." );
                return false;
            }
            bool success = true;
            foreach( var o in maps.EnumerateObject() )
            {
                if( !ReadPath( monitor, o.Value, out var target ) )
                {
                    success = false;
                    continue;
                }
                if( !resources.TryGetResource( o.Name, out var res ) )
                {
                    if( !resources.TryGetFolder( o.Name, out var resFolder ) )
                    {
                        monitor.Error( $"Invalid mapping \"{o.Name}\". This resource or folder doesn't exist." );
                        success = false;
                    }
                    else
                    {
                        foreach( var r in resFolder.AllResources )
                        {
                            success &= AddFinal( monitor, o.Name, target, resFolder, r, ref overrides, final );
                        }
                    }
                }
                else
                {
                    success &= AddFinal( monitor, o.Name, target, resources, res, ref overrides, final );
                }
            }
            return success;
        }

        static bool AddFinal( IActivityMonitor monitor,
                              string? mappingKey,
                              NormalizedPath target,
                              ResourceFolder resFolder,
                              ResourceLocator res,
                              ref Dictionary<NormalizedPath, ResourceOverrideKind>? overrides,
                              Dictionary<NormalizedPath, ResourceAssetDefinition> final )
        {
            var subPath = res.FullResourceName.Substring( resFolder.FullFolderName.Length );
            var t = target.Combine( subPath );
            if( final.TryGetValue( target, out var already ) )
            {
                if( mappingKey != null )
                {
                    monitor.Error( $"Invalid mapping \"{mappingKey}\": \"{target}\". Resource '{already.Origin.ResourceName}' maps to the same target '{t}'." );
                }
                else
                {
                    monitor.Error( $"Resource '{subPath}' maps to '{t}' but '{already.Origin.ResourceName}' already maps to the same target." );
                }
                return false;
            }
            ResourceOverrideKind o = ResourceOverrideKind.None;
            if( overrides != null )
            {
                if( overrides.TryGetValue( t, out o ) )
                {
                    overrides.Remove( t );
                    if( overrides.Count == 0 ) overrides = null;
                }
            }
            final.Add( t, new ResourceAssetDefinition( res, o ) );
            return true;
        }

        static bool HandleOverrides( IActivityMonitor monitor,
                                     string oName,
                                     ResourceOverrideKind overrideKind,
                                     JsonElement names,
                                     ref Dictionary<NormalizedPath, ResourceOverrideKind>? overrides )
        {
            if( names.ValueKind is not JsonValueKind.Array )
            {
                monitor.Error( $"Invalid \"{oName}\": it must be an array with resource names." );
                return false;
            }
            bool success = true;
            foreach( var o in names.EnumerateArray() )
            {
                if( !ReadPath( monitor, o, out var path ) || path.IsEmptyPath )
                {
                    monitor.Error( $"Invalid override in \"{oName}\": {o} must be a resource name." );
                    success = false;
                    continue;
                }
                success &= AddOverride( monitor, oName, path, overrideKind, ref overrides );
            }
            return success;

            static bool AddOverride( IActivityMonitor monitor,
                                     string sOverrideKind,
                                     NormalizedPath path,
                                     ResourceOverrideKind overrideKind,
                                     ref Dictionary<NormalizedPath, ResourceOverrideKind>? overrides )
            {
                if( overrides == null )
                {
                    overrides = new Dictionary<NormalizedPath, ResourceOverrideKind>();
                }
                else
                {
                    if( overrides.TryGetValue( path, out var already ) && already != overrideKind )
                    {
                        monitor.Error( $"Invalid override \"{sOverrideKind}\": [... \"{path}\" ...]. This is already defined as a {already} override." );
                        return false;
                    }
                }
                overrides.Add( path, overrideKind );
                return true;
            }
        }

        static bool ReadPath( IActivityMonitor monitor, JsonElement jsonTargetPath, out NormalizedPath path )
        {
            if( jsonTargetPath.ValueKind != JsonValueKind.String )
            {
                monitor.Error( $"Invalid path: {jsonTargetPath} must be a string." );
                path = default;
                return false;
            }
            path = jsonTargetPath.GetString();
            if( path.RootKind != NormalizedPathRootKind.None )
            {
                if( path.RootKind is NormalizedPathRootKind.RootedByFirstPart or NormalizedPathRootKind.RootedByURIScheme )
                {
                    monitor.Error( $"Invalid path '{path}'. A target path must be relative (same as if it start with '/')." );
                    return false;
                }
                path = path.With( NormalizedPathRootKind.None );
            }
            return true;
        }
    }

}
