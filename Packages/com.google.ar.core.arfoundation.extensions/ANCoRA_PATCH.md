# ANCoRA compatibility patch

This is the official Google ARCore Extensions `v1.56.0-arf6` package, embedded so
the project can apply one reproducible Unity 6.6 compatibility patch.

`Editor/Scripts/Internal/Analytics/Google.Protobuf.dll` was replaced with the
`Google.Protobuf.dll` distributed with Unity `6000.6.0f1`. The upstream package
bundles an older assembly with the same identity. Unity can load that copy before
its own MSBuild host and then fails to resolve `Google.Protobuf.IBufferMessage`,
breaking script compilation with a `MsBuildCompilation` `TypeLoadException`.

When upgrading Unity or ARCore Extensions, remove this embedded package, test the
new official package first, and reapply the replacement only if the upstream
conflict still exists.
