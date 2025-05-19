using CK.Core;

namespace CK.EmbeddedResources;

/// <summary>
/// A resource asset definition is a resource and a <see cref="ResourceOverrideKind"/> and
/// describes the content of the target path that is indexed by <see cref="ResourceAssetDefinitionSet.Assets"/>.
/// </summary>
/// <param name="Origin">The resource itself.</param>
/// <param name="Override">The override kind.</param>
public readonly record struct ResourceAssetDefinition( ResourceLocator Origin, ResourceOverrideKind Override );
