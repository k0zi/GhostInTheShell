# Third-party notices

Ghost in the Shell's own source code is licensed under the MIT license in LICENSE.
The .deb and .rpm packages are self-contained builds and also redistribute the third-party
components listed below. These components keep their own licenses, and the full license texts are in
`licenses/`. Listing them here does not relicense them or imply endorsement.

## Redistributed libraries (self-contained linux-x64 build)

| Component | Source | License / local copy |
| --- | --- | --- |
| .NET runtime 10.0.15 (self-contained runtime pack) Microsoft.Extensions.DependencyInjection(.Abstractions) 10.0.9 and System.Composition 10.0.9 (MEF, plugin loading) | https://github.com/dotnet/runtime | MIT: licenses/dotnet-runtime-MIT.txt; upstream component notices: licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt |
| Avalonia 12.1.3 (Avalonia, Avalonia.Desktop, Avalonia.FreeDesktop, Avalonia.FreeDesktop.AtSpi, Avalonia.HarfBuzz, Avalonia.Native, Avalonia.Remote.Protocol, Avalonia.Skia, Avalonia.Themes.Fluent, Avalonia.Win32, Avalonia.X11, Avalonia.Fonts.Inter) | https://github.com/AvaloniaUI/Avalonia | MIT: licenses/Avalonia-MIT.txt; upstream component notices: licenses/Avalonia-NOTICE.txt |
| Inter font, bundled in Avalonia.Fonts.Inter 12.1.3 | https://github.com/rsms/inter | OFL-1.1: licenses/Inter-OFL-1.1.txt (applies to the font, separately from Avalonia's MIT wrapper) |
| SkiaSharp 3.119.4 and HarfBuzzSharp 8.3.1.3, with their Linux native libraries (SkiaSharp, SkiaSharp.NativeAssets.Linux, HarfBuzzSharp, HarfBuzzSharp.NativeAssets.Linux) | https://github.com/mono/SkiaSharp | MIT: licenses/SkiaSharp-MIT.txt; native component notices (Skia, HarfBuzz, libpng, …): licenses/SkiaSharp-THIRD-PARTY-NOTICES.txt |
| CommunityToolkit.Mvvm 8.4.2 | https://github.com/CommunityToolkit/dotnet | MIT: licenses/CommunityToolkit-MIT.txt; upstream component notices: licenses/CommunityToolkit-THIRD-PARTY-NOTICES.txt |
| KD.Avalonia.Rice 26.10.32 | https://github.com/k0zi/KD.Avalonia.Rice | MIT: licenses/KD.Avalonia.Rice-MIT.txt; theme palettes below |
| Material.Icons and Material.Icons.Avalonia 3.0.2 | https://github.com/SKProCH/Material.Icons | Wrapper code, MIT: licenses/Material.Icons-MIT.txt |
| Material Design Icons (icon paths compiled into Material.Icons) | https://github.com/Templarian/MaterialDesign (Pictogrammers) | Pictogrammers Free License: licenses/Pictogrammers-Free-License.txt; Apache-2.0: licenses/Apache-2.0.txt |
| MicroCom.Runtime 0.11.6 | https://github.com/kekekeks/MicroCom | MIT: licenses/MicroCom-MIT.txt |
| Tmds.DBus.Protocol 0.94.1 | https://github.com/tmds/Tmds.DBus | MIT: licenses/Tmds.DBus-MIT.txt |

### Theme palettes (in KD.Avalonia.Rice)

The themes offered in **Settings** use color palettes adapted from these color schemes:

| Palette | Source | License copy |
| --- | --- | --- |
| Nord, Sven Greb | https://github.com/nordtheme/nord | licenses/Nord-MIT.txt |
| Solarized, Ethan Schoonover | https://github.com/altercation/solarized | licenses/Solarized-MIT.txt |
| Dracula, Dracula Theme | https://github.com/dracula/dracula-theme | licenses/Dracula-MIT.txt |
| Gruvbox, morhetz | https://github.com/morhetz/gruvbox | licenses/Gruvbox-MIT.txt |
| Catppuccin | https://github.com/catppuccin/palette | licenses/Catppuccin-MIT.txt |

## Project artwork

The application icon (`packaging/ghostintheshell.png`, also embedded as
`src/GhostInTheShell.App/Assets/ghostintheshell.png`) and its drafts (`packaging/ghost-terminal-v*.png`)
were generated for this project with an AI image generator. To the extent the project's contributors hold
rights in it, it is available under this repository's MIT license.

## Trademarks

Names of Linux distributions (Ubuntu, Fedora, Manjaro), color schemes (Nord, Dracula, Solarized, Gruvbox,
Catppuccin, …), coding agents and other products appear in the user interface and documentation only to
identify them. They are trademarks of their respective owners, and no affiliation or endorsement is claimed.

## Not redistributed

The following are used only to build or test Ghost in the Shell and are not included in the packages:
Nerdbank.GitVersioning, xunit (xunit, xunit.core, xunit.assert, xunit.abstractions, xunit.analyzers,
xunit.extensibility.*), xunit.runner.visualstudio, Microsoft.NET.Test.Sdk (with Microsoft.CodeCoverage,
Microsoft.TestPlatform.*), coverlet.collector and Newtonsoft.Json. Platform-specific packages for other
operating systems (Windows/macOS/WebAssembly native assets of SkiaSharp and HarfBuzzSharp, ANGLE for
Windows) are restored but not part of the Linux packages.

## Maintenance

When you add copied code or assets, or change the dependencies, record the source, version,
author and license here, and update the license copies in `licenses/`. Before a release, check
the publish output (`dotnet publish -r linux-x64 --self-contained`) for new native libraries.
