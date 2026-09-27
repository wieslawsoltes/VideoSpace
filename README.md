# VideoSpace

A local-first nonlinear video editor built with Uno Platform, reusable .NET libraries and GPU compositing. VideoSpace follows familiar professional editing workflows with original branding and assets.

The initial implementation series is in progress. See the commit history and Actions for executable validation. Do not treat this initial version as complete Adobe Premiere Pro parity.

## Engine validation

Requires the .NET SDK pinned in `global.json`.

```sh
dotnet run --project tests/VideoSpace.Tests -c Release
```

## Independence

VideoSpace is an independent MIT-licensed project and is not affiliated with Adobe. Adobe Premiere Pro is a trademark of Adobe. No Adobe code, icons, fonts or footage are included.
