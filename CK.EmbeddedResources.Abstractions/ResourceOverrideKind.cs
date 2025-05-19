namespace CK.Core;

/// <summary>
/// Resources carried by a package are almost always combined with other resources (from
/// dependent packages). This enumeration is defined here because it is used by different
/// kind of resource implementations.
/// <para>
/// When expressed via strings, "O", "?O" or "!O" strings (or string prefixes "O:", "?O:" or "!O:")
/// should be used.
/// </para>
/// </summary>
public enum ResourceOverrideKind : byte
{
    /// <summary>
    /// No override: the defined resource must not already exist.
    /// This default mode prevents any unattended rewrite of existing resources.
    /// </summary>
    None,

    /// <summary>
    /// Regular override ("O") is the safest one: the resource must
    /// already exist. This guaranties that if the dependency evolves by removing the
    /// resource, an error can be raised (or a warning is raised and the resource ignored).
    /// </summary>
    Regular,

    /// <summary>
    /// Optional override ("?O") declares that a resource is overridden only if it already
    /// exists. No warning of any kind must be emitted if the resource doesn't already exist
    /// and the resource is simply ignored.
    /// </summary>
    Optional,

    /// <summary>
    /// Always override ("!O") adds the resource whether it exists or not.
    /// This is the most risky mode to consider.
    /// </summary>
    Always
}
