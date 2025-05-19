using CK.Core;
using System;
using System.Linq;

namespace CK.EmbeddedResources;

/// <summary>
/// Extends <see cref="IResourceContainer"/>.
/// </summary>
public static class ResourceContainerExtensions
{
    /// <summary>
    /// Gets the local file path of the root of this container.
    /// </summary>
    /// <param name="container">This container.</param>
    /// <returns>The container's local path or null.</returns>
    public static string? GetLocalPath( this IResourceContainer container )
    {
        if( container.HasLocalFilePathSupport )
        {
            var root = new ResourceLocator( container, container.ResourcePrefix );
            return container.GetLocalFilePath( root );
        }
        return null;
    }

    /// <summary>
    /// Tries to get an existing resource and logs an error if it is not found.
    /// </summary>
    /// <param name="container">This container.</param>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="resourceName">The local resource name (can contain any folder prefix).</param>
    /// <param name="locator">The resulting locator.</param>
    /// <returns>True if the resource exists, false otherwise.</returns>
    public static bool TryGetExpectedResource( this IResourceContainer container,
                                               IActivityMonitor monitor,
                                               string resourceName,
                                               out ResourceLocator locator )
    {
        if( !container.TryGetResource( resourceName, out locator ) )
        {
            monitor.Error( $"Unable to find expected resource '{resourceName}' from {container.DisplayName}." );
            return false;
        }
        return true;
    }

    /// <summary>
    /// Tries to get an existing resource in this container and <paramref name="otherContainers"/>.
    /// This logs an error if it is not found.
    /// </summary>
    /// <param name="container">This container.</param>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="resourceName">The local resource name (can contain any folder prefix).</param>
    /// <param name="locator">The resulting locator.</param>
    /// <param name="otherContainers">Any other containers to lookup.</param>
    /// <returns>True if the resource exists, false otherwise.</returns>
    public static bool TryGetExpectedResource( this IResourceContainer container,
                                               IActivityMonitor monitor,
                                               string resourceName,
                                               out ResourceLocator locator,
                                               params IResourceContainer[] otherContainers )
    {
        if( otherContainers.Length == 0 )
        {
            return TryGetExpectedResource( container, monitor, resourceName, out locator );
        }
        if( container.TryGetResource( resourceName, out locator ) )
        {
            return true;
        }
        foreach( var c in otherContainers )
        {
            if( container.TryGetResource( resourceName, out locator ) )
            {
                return true;
            }
        }
        monitor.Error( $"Unable to find expected resource '{resourceName}' from {container.DisplayName} and {otherContainers.Select( c => c.DisplayName ).Concatenate()}." );
        return false;
    }

    /// <summary>
    /// Tries to get an existing resource.
    /// </summary>
    /// <param name="container">This container.</param>
    /// <param name="resourceName">The local resource name (can contain any folder prefix).</param>
    /// <param name="locator">The resulting locator.</param>
    /// <returns>True if the resource exists, false otherwise.</returns>
    public static bool TryGetResource( this IResourceContainer container, ReadOnlySpan<char> resourceName, out ResourceLocator locator )
    {
        locator = container.GetResource( resourceName );
        return locator.IsValid;
    }

    /// <summary>
    /// Tries to get an existing folder.
    /// </summary>
    /// <param name="container">This container.</param>
    /// <param name="folderName">The local resource folder name (can contain any folder prefix).</param>
    /// <param name="folder">The resulting folder.</param>
    /// <returns>True if the folder exists, false otherwise.</returns>
    public static bool TryGetFolder( this IResourceContainer container,  ReadOnlySpan<char> folderName, out ResourceFolder folder )
    {
        folder = container.GetFolder( folderName );
        return folder.IsValid;
    }

    /// <summary>
    /// Tries to get an existing resource in this folder.
    /// </summary>
    /// <param name="parentFolder">This folder.</param>
    /// <param name="resourceName">The local resource name (can contain any folder prefix).</param>
    /// <param name="locator">The resulting locator.</param>
    /// <returns>True if the resource exists, false otherwise.</returns>
    public static bool TryGetResource( this ResourceFolder parentFolder, ReadOnlySpan<char> resourceName, out ResourceLocator locator )
    {
        locator = parentFolder.GetResource( resourceName );
        return locator.IsValid;
    }

    /// <summary>
    /// Tries to get an existing folder in this folder.
    /// </summary>
    /// <param name="parentFolder">This parent folder.</param>
    /// <param name="folderName">The local resource folder name (can contain any folder prefix).</param>
    /// <param name="folder">The resulting folder.</param>
    /// <returns>True if the folder exists, false otherwise.</returns>
    public static bool TryGetFolder( this ResourceFolder parentFolder,  ReadOnlySpan<char> folderName, out ResourceFolder folder )
    {
        folder = parentFolder.GetFolder( folderName );
        return folder.IsValid;
    }
}
