using CK.Core;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.EmbeddedResources;

/// <summary>
/// Extends <see cref="Type"/>.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Creates a <see cref="AssemblyResourceContainer"/> or <see cref="FileSystemResourceContainer"/> if this assembly
    /// is a local one for a type that must be decorated with at least one <see cref="IEmbeddedResourceTypeAttribute"/>
    /// attribute.
    /// <para>
    /// On success, the container is bound to the corresponding "Res/" (or "Res[After]/") folder from embedded ressources
    /// or the file system local folder.
    /// </para>
    /// <para>
    /// It may not be <see cref="IResourceContainer.IsValid"/> (an error has been logged).
    /// </para>
    /// </summary>
    /// <param name="type">This type.</param>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="containerDisplayName">When null, the <see cref="IResourceContainer.DisplayName"/> defaults to "resources of '...' type".</param>
    /// <param name="resAfter">True to consider the "Res[After]" folder instead of "Res".</param>
    /// <param name="ignoreLocal">
    /// True to ignore local folder: if the resources are in a local folder, this creates
    /// a <see cref="AssemblyResourceContainer"/> instead of a <see cref="FileSystemResourceContainer"/>.
    /// <para>
    /// This is mainly for tests.
    /// </para>
    /// </param>
    /// <returns>The resources (<see cref="IResourceContainer.IsValid"/> may be false).</returns>
    public static IResourceContainer CreateResourcesContainer( this Type type,
                                                               IActivityMonitor monitor,
                                                               string? containerDisplayName = null,
                                                               bool resAfter = false,
                                                               bool ignoreLocal = false )
    {
        return AssemblyResources.GetCallerInfo( monitor,
                                                type,
                                                type.GetCustomAttributes().OfType<IEmbeddedResourceTypeAttribute>(),
                                                out var callerPath,
                                                out var callerSource )
                ? CreateResourcesContainer( type, monitor, callerPath, callerSource.GetType().Name, containerDisplayName, resAfter, ignoreLocal )
                : new EmptyResourceContainer( AssemblyResourceContainer.MakeDisplayName( containerDisplayName, type, resAfter ),
                                              isDisabled: false,
                                              resourcePrefix: "",
                                              isValid: false );
    }

    /// <summary>
    /// Creates a <see cref="AssemblyResourceContainer"/> or <see cref="FileSystemResourceContainer"/> if the type's assembly
    /// is a local one for based on a CallerFilePath (<see cref="CallerFilePathAttribute"/>)
    /// captured by an attribute on this type (typically a <see cref="IEmbeddedResourceTypeAttribute"/>).
    /// <para>
    /// On success, the container is bound to the corresponding embedded ressources "Res/" folder or to the file system
    /// "Res\" folder.
    /// </para>
    /// <para>
    /// It may not be <see cref="IResourceContainer.IsValid"/> (an error has been logged).
    /// </para>
    /// </summary>
    /// <param name="type">This type.</param>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="callerFilePath">The caller file path. This is required: when null or empty, an error is logged and an invalid container is returned.</param>
    /// <param name="attributeName">The attribute name that declares the resource (used for logging).</param>
    /// <param name="containerDisplayName">When null, the <see cref="IResourceContainer.DisplayName"/> defaults to "resources of '...' type".</param>
    /// <param name="resAfter">True to consider the "Res[After]" folder instead of "Res".</param>
    /// <param name="ignoreLocal">
    /// True to ignore local folder: even if the resources are in a local folder, this creates
    /// a <see cref="AssemblyResourceContainer"/> instead of a <see cref="FileSystemResourceContainer"/>.
    /// <para>
    /// This is mainly for tests.
    /// </para>
    /// </param>
    /// <returns>The resources (<see cref="IResourceContainer.IsValid"/> may be false).</returns>
    static public IResourceContainer CreateResourcesContainer( this Type type,
                                                               IActivityMonitor monitor,
                                                               string? callerFilePath,
                                                               string attributeName,
                                                               string? containerDisplayName = null,
                                                               bool resAfter = false,
                                                               bool ignoreLocal = false )
    {
        var assembly = type.Assembly;
        var assemblyName = assembly.GetName().Name;
        Throw.CheckArgument( "Cannot handle dynamic assembly.", assemblyName != null );
        if( !ignoreLocal && LocalDevSolution.LocalProjectPaths.TryGetValue( assemblyName, out var projectPath ) )
        {
            containerDisplayName = AssemblyResourceContainer.MakeDisplayName( containerDisplayName, type, resAfter );
            if( AssemblyResources.ValidateContainerForType( monitor,
                                                            assembly,
                                                            assemblyName,
                                                            callerFilePath,
                                                            type,
                                                            attributeName,
                                                            out var subPath ) )
            {
                return new FileSystemResourceContainer( projectPath.Combine( subPath ).AppendPart( resAfter ? "Res[After]" : "Res" ), containerDisplayName );
            }
            return new EmptyResourceContainer( containerDisplayName, isDisabled: false, resourcePrefix: projectPath, isValid: false );
        }
        return AssemblyExtensions.GetResources( assembly, assemblyName )
                                  .CreateResourcesContainerForType( monitor,
                                                                    callerFilePath,
                                                                    type,
                                                                    attributeName,
                                                                    containerDisplayName,
                                                                    resAfter );
    }


}
