# CK.EmbeddedResources

Provides an abstraction for "resources" identified by a string that can exist
as embedded resources in assembly, be files on the local file system or dynamically
created by code.

## Content of the solution
### `CK.EmbeddedResources.Abstractions`
Provides automatic embedded resources support for `Res/` and `Res[After]/` folders
thanks to the [MSBuild](CK.EmbeddedResources.Abstractions/MSBuild/) props and targets
files that transitively flow accross dependencies.

It is a micro-package that only defines 3 types:
- [`IEmbeddedResourceTypeAttribute`](CK.EmbeddedResources.Abstractions/IEmbeddedResourceTypeAttribute.cs) is an interface that can be implemented by attributes.
- [`EmbeddedResourceTypeAttribute`](CK.EmbeddedResources.Abstractions/EmbeddedResourceTypeAttribute.cs) is the default implementation of this interface.
- [`ResourceOverrideKind`](CK.EmbeddedResources.Abstractions/ResourceOverrideKind.cs) is an enum that models all possible behaviors when dealing with overrides of a resource accross packages.

### `CK.EmbeddedResources`
Is a bigger package that implements resource containers. Resource containers are
a "File System" with resources in folders identified by a hierarchical path. 
The [`IResourceContainer`](CK.EmbeddedResources/IResourceContainer.cs) is the common
abstraction to different concrete containers:
- The `AssemblyResourceContainer` exposes embedded resources in assembly.
- The `FileSystemResourceContainer` exposes local file system folders and files as resources.
- The `CodeGenResourceContainer` exposes code generated content as resources.
- `EmptyResourceContainer` and `ResourceContainerWrapper` are rather classical helpers for such abstractions.

Resources are identified by `ResourceLocator` and `ResourceFolder` that are 2 small structs
managed by their owning container.

_Note:_ This abstraction is not perfect. The "folder separator" should be abstracted more than
it currenly is: today, only '/' or '\' is really supported, but this is enough for our needs.

### `CK.EmbeddedResources.Assets`
Implements _assets resources_: their definition and their
composition accross multiple sources in a Direct Acyclic Graph structure.



