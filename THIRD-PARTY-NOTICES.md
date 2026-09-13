# Third-Party Notices

KT MD Viewer is released under the MIT License (see [LICENSE](LICENSE)).

It ships with, or builds upon, the third-party components listed below. Each
component remains under its own license, and those licenses continue to apply to
the component in question. The authoritative license text for every package is
the one contained in the package itself (NuGet packages carry it in the package
folder) or at the project link given here.

## Packages

| Component | Version | License | Project |
| --- | --- | --- | --- |
| Avalonia | 12.1.2 | MIT | <https://github.com/AvaloniaUI/Avalonia> |
| Avalonia.Desktop | 12.1.2 | MIT | <https://github.com/AvaloniaUI/Avalonia> |
| Avalonia.Themes.Fluent | 12.1.2 | MIT | <https://github.com/AvaloniaUI/Avalonia> |
| Avalonia.Fonts.Inter | 12.1.2 | MIT (package), SIL OFL 1.1 (font) | <https://github.com/AvaloniaUI/Avalonia> |
| Avalonia.Diagnostics (Debug builds only, not shipped) | 11.3.21 | MIT | <https://github.com/AvaloniaUI/Avalonia> |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | <https://github.com/CommunityToolkit/dotnet> |
| Markdig | 1.3.2 | BSD 2-Clause | <https://github.com/xoofx/markdig> |
| TextMateSharp | 2.0.4 | MIT | <https://github.com/danipen/TextMateSharp> |
| TextMateSharp.Grammars | 2.0.4 | MIT (grammars keep their upstream licenses, mainly MIT) | <https://github.com/danipen/TextMateSharp> |
| Mermaider | 0.12.2 | MIT (SPDX expression in package metadata; © Nullean) | <https://github.com/nullean/mermaider> |
| Sugiyama | 0.12.2 | MIT (SPDX expression in package metadata; © Nullean) | <https://github.com/nullean/mermaider> |
| Svg.Controls.Skia.Avalonia (Svg.Skia) | 12.0.0.17 | MIT | <https://github.com/wieslawsoltes/Svg.Skia> |
| Sylinko.CSharpMath.Avalonia (CSharpMath) | 12.0.0 | MIT | <https://github.com/verybadcat/CSharpMath> |

Transitive dependencies pulled in by the packages above — including SkiaSharp and
HarfBuzzSharp (MIT), `Microsoft.Extensions.ObjectPool` and the Microsoft .NET
runtime libraries (MIT) — are covered by their own licenses as published with
those packages.

## Fonts

The Inter typeface, embedded through `Avalonia.Fonts.Inter`, is licensed under the
SIL Open Font License 1.1: <https://github.com/rsms/inter>.

## Reporting

If you believe a component is missing from this list or is attributed
incorrectly, please open an issue at
<https://github.com/hansos/MdViewer/issues>.
