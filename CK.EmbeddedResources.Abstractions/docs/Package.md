Automatic embedded resources support for the `Res/` and `Res[After]/` folders of a project.

A micro-package with three types and a pair of MSBuild props and targets that flow transitively across
dependencies: every file under those folders becomes an embedded resource whose logical name is its
project-relative path prefixed by "ck@".

IEmbeddedResourceTypeAttribute captures the source file path of the attribute decorating a type, which
is what maps a type back to the resource folder next to it. ResourceOverrideKind models the four ways a
package can redefine a resource that another package already defines.
