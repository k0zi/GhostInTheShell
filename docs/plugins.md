# Writing a component plugin

A plugin is a .NET assembly that adds agents or development environments (*components*) to Ghost in the Shell, without changing or rebuilding the app. The app finds plugins at startup with System.Composition (MEF).

If you only need install commands, a JSON component in `~/.config/ghostintheshell/components/` is simpler (see the README). Write a plugin when you need logic, or want to ship components on your own release schedule, for example as a distro package.

## The contract

Reference `GhostInTheShell.Plugins.Abstractions` and export one class per component:

```csharp
using System.Composition;
using GhostInTheShell.Plugins;

[Export(typeof(IContainerComponent))]
public sealed class ZigToolchain : ContainerComponentBase
{
    public override string Id => "zig";                       // stored with each machine; keep it stable
    public override string DisplayName => "Zig";
    public override string Description => "Zig compiler";
    public override ComponentKind Kind => ComponentKind.Toolchain;   // or ComponentKind.Agent
    public override int Order => 600;                         // position within its kind (bundled ones use 100..500)
    public override IReadOnlyList<string> UserPath { get; } = ["~/.zig/bin"];
    public override string? Check => "zig version";           // fails the image build if the install broke

    // Root commands per distro family ("debian", "fedora", "arch"); "*" matches any family.
    protected override IReadOnlyDictionary<string, string> Root { get; } = new Dictionary<string, string>
    {
        ["*"] = "curl -fsSL https://example.com/zig.tar.xz | tar -xJ -C /opt",
    };

    // Runs as the machine user, after every root command.
    protected override string? User => null;
}
```

`ContainerComponentBase` covers the common case. For full control, implement `IContainerComponent` directly:

| Member | Meaning |
|---|---|
| `Id`, `DisplayName`, `Description` | identity and the text shown in the new machine dialog |
| `Kind` | `Toolchain` layers come before `Agent` layers |
| `Order` | sort key within the kind; together with `Id` it fixes the layer order, and so the image tag |
| `SupportsOs(TargetOs)` | false greys the option out for that distribution |
| `GetRootCommand(TargetOs)` | shell command run as root (one image layer), or null |
| `GetUserCommand(TargetOs)` | shell command run as the machine user, or null |
| `Check` | appended to the user command with `&&` |
| `UserPath` | directories added to `PATH` when the component is selected; `~` is the user's home |

`TargetOs` carries the distribution's `Id` (for example `ubuntu-24.04`) and `Family`.

Return the same commands every time for the same input: the image tag is a hash of the generated Containerfile, so changing output means a rebuild.

## Project file

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>
  <ItemGroup>
    <!-- The app supplies the contracts at run time: do not copy them next to the plugin. -->
    <ProjectReference Include="path/to/GhostInTheShell.Plugins.Abstractions.csproj">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </ProjectReference>
  </ItemGroup>
</Project>
```

[`samples/GhostInTheShell.SamplePlugin`](../samples/GhostInTheShell.SamplePlugin) is a complete example that adds a Go toolchain. Every agent and development environment the app ships with is a plugin too, one project each under [`src/Plugins`](../src/Plugins).

### Adding a bundled plugin

To ship a new component with the app, add a project folder `src/Plugins/GhostInTheShell.Plugins.<Name>/` with an empty `GhostInTheShell.Plugins.<Name>.csproj` (the folder, project and assembly names must match; `src/Plugins/Directory.Build.props` supplies the settings and the contracts reference) and the component class, and add the project to `GhostInTheShell.slnx`. `src/Plugins/BundledPlugins.targets` picks up every project there and copies it into `plugins/` of the app's build and publish output.

## Installing

Copy the build output into a folder named after the assembly:

```
~/.config/ghostintheshell/plugins/
  MyPlugin/
    MyPlugin.dll          # must match the folder name
    MyPlugin.deps.json
    SomeDependency.dll    # the plugin's own dependencies, if any
```

System-wide plugins (for example from a distro package) go to `/usr/lib/ghostintheshell/plugins/<Name>/` instead. Restart the app to load new plugins.

Each plugin gets its own `AssemblyLoadContext`, so its dependencies do not clash with the app's or with other plugins'. Only `GhostInTheShell.Plugins.Abstractions` and `System.Composition.*` are always taken from the app.

## Precedence and errors

The app loads components in this order:

1. the bundled plugins (`plugins/` next to the executable);
2. system-wide plugins, then the user's plugins (each folder in name order);
3. components still defined in an old-format `catalog.json`;
4. JSON components.

A later component with the same `Id` replaces an earlier one, so a plugin can replace a bundled component and a JSON file can patch both. For an installed package the bundled folder and the system-wide folder are the same, `/usr/lib/ghostintheshell/plugins/`; it is loaded once.

A plugin that cannot be loaded (not an assembly, missing dependency, no export, composition error) is skipped and reported in a notification; everything else still loads.

## Compatibility

The contract assembly's version is pinned to `1.0` and changes only on a breaking change to the interfaces, so a plugin keeps working across app releases until then.
