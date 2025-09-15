# Changelog

## [1.3.0] - 2025-01-15

### Added
- .NET 8.0 support and migration from .NET 6.0/7.0
- GitHub Actions CI/CD workflow for automated builds and tests
- Directory.Build.props with .NET 8 recommended settings
- global.json to pin .NET SDK to 8.0.100

### Changed
- Updated all projects to target .NET 8.0 (net8.0)
- Upgraded NuGet packages to versions compatible with .NET 8.0:
  - Microsoft.NET.Test.Sdk: 17.3.2 → 17.8.0
  - NUnit: 3.13.3 → 4.0.1
  - NUnit3TestAdapter: 4.3.0 → 4.5.0
  - NUnit.Analyzers: 3.5.0 → 3.10.0
  - coverlet.collector: 3.1.2 → 6.0.0
- Version bumped to 1.3.0 to reflect .NET 8 upgrade

### Fixed
- Fixed nullable reference assignment warning in RegisterMapBuilder.cs
- Fixed ref/in parameter warnings in IRegisterMap.cs for .NET 8 compatibility
- Improved memory safety by using proper parameter passing for Span operations

### Removed
- Removed multi-targeting (net6.0;net7.0) in favor of single .NET 8.0 target

## [1.2.0] - Previous Release
- Legacy .NET 6.0/7.0 support