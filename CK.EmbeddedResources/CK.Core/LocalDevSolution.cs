using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CK.Core;

/// <summary>
/// Tries to detect a "under development" solution and locates the local projects.
/// </summary>
public static class LocalDevSolution
{
    static readonly HashSet<string>? _paths;

    /// <summary>
    /// Gets the solution root folder based on <see cref="AppContext.BaseDirectory"/>
    /// and the existence of a /.git folder.
    /// </summary>
    public static readonly NormalizedPath SolutionFolder;

    /// <summary>
    /// Gets whether the <see cref="SolutionFolder"/> has been found and a ".sln" or ".slnx" file that follows
    /// the basic naming convention (it must be named with the name of the git working folder like "CK-EmbeddedResources/CK-EmbeddedResources.slnx")
    /// exists and contains at least one C# project.
    /// <para>
    /// When false, it is useless to call <see cref="FindLocalProjectPath(Assembly, out NormalizedPath)"/>.
    /// </para>
    /// </summary>
    public static bool HasLocalProjects => _paths != null;

    /// <summary>
    /// Tries to find the local full path project folder (the folder that contains the ".csproj" file
    /// that is the source code of the <paramref name="assembly"/>).
    /// <para>
    /// <see cref="HasLocalProjects"/> must be true.
    /// </para>
    /// </summary>
    /// <param name="assembly">The assembly that may be a locally defined one.</param>
    /// <param name="projectPath">Contains the local project folder on success.</param>
    /// <returns>Whether a local folder has been found for the assembly.</returns>
    public static bool FindLocalProjectPath( Assembly assembly, out NormalizedPath projectPath )
    {
        projectPath = default;
        if( _paths != null )
        {
            var path = (string?)assembly.CustomAttributes.FirstOrDefault( a => a.AttributeType == typeof( AssemblyMetadataAttribute )
                                                                               && (string?)a.ConstructorArguments[0].Value == "SolutionRelativeProjectPath" )?
                                                         .ConstructorArguments[1].Value;
            if( path != null )
            {
                path = FileUtil.NormalizePathSeparator( path, ensureTrailingBackslash: false );
                if( _paths.Contains( path ) )
                {
                    projectPath = SolutionFolder.Combine( Path.GetDirectoryName( path ) );
                    return true;
                }
            }
        }
        return false;
    }

    static LocalDevSolution()
    {
        var p = GetSolutionDir();
        HashSet<string>? projectsPath = null;
        if( !p.IsEmptyPath )
        {
            SolutionFolder = p;
            var slnText = ReadSlnFile( p );
            if( slnText != null )
            {
                // One time regex. Don't cache.
                var projects = Regex.Matches( slnText, @"(?<="")[^""]*\.csproj(?="")" );
                foreach( Match project in projects )
                {
                    var path = project.Value;
                    var fullPath = Path.Combine( p, project.Value );
                    if( !File.Exists( fullPath ) )
                    {
                        ActivityMonitor.StaticLogger.Warn( $"Project file '{fullPath}' declared in solution file not found. Ignoring project." );
                    }
                    else
                    {
                        projectsPath ??= new HashSet<string>();
                        path = FileUtil.NormalizePathSeparator( path, ensureTrailingBackslash: false );
                        if( !projectsPath.Add( path ) )
                        {
                            ActivityMonitor.StaticLogger.Warn( $"Found duplicate project '{path}' in solution file. Ignoring project '{fullPath}'." );
                        }
                    }
                }
                if( projectsPath == null )
                {
                    ActivityMonitor.StaticLogger.Warn( $"No project found in solution file:{Environment.NewLine}{slnText}." );
                }
                else
                {
                    _paths = projectsPath;
                }
            }
        }

        static NormalizedPath GetSolutionDir()
        {
            var p = AppContext.BaseDirectory;
            while( !string.IsNullOrEmpty( p ) )
            {
                if( Directory.Exists( Path.Combine( p, ".git" ) ) )
                {
                    return p;
                }
                p = Path.GetDirectoryName( p );
            }
            return default;
        }

        static string? ReadSlnFile( NormalizedPath solutionFolder )
        {
            var slnPath = solutionFolder.AppendPart( solutionFolder.LastPart + ".sln" );
            if( File.Exists( slnPath ) )
            {
                return File.ReadAllText( slnPath );
            }
            var slnxPath = slnPath.Path + 'x';
            if( File.Exists( slnxPath ) )
            {
                return File.ReadAllText( slnxPath );
            }
            ActivityMonitor.StaticLogger.Warn( $"Unable to find expected '{slnPath}' file. No local projects can be handled." );
            return null;
        }
    }
}
