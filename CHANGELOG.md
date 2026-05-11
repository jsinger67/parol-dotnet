# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project aims to follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

## [0.3.0] - 2026-05-11

### Added
- Add scanner-mode-specific skip token support in `Scanner.Scan`.
    - New overload accepts `skipTokensByMode` to align runtime behavior with `%skip` per scanner state.
    - Existing `Scanner.Scan` overload keeps backward-compatible default skipping for token types `1..4`.

## [0.2.1] - 2026-04-03

### Changed
- Update logo path in README to use absolute URL for proper display
- Update project description and tags to include LALR(1) support
- Update README to clarify support for LL(k) and LALR(1) parsers

## [0.2.0] - 2026-03-15

### Added
- Multi-target NuGet package support for `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0`.

## [0.1.3] - 2026-03-14

### Added
- Added corresponding LR parser runtime support for generated C# LALR(1) parsers.

## [0.1.2] - 2026-03-08

- Improve structured error reporting
    - Enhance LLKParser with source name support for improved diagnostics and add
    ParserDiagnosticLocation
    - Provide new exception types for different error categories


## [0.1.1] - 2026-02-18

### Added
- Dual licensing metadata and texts (`Apache-2.0 OR MIT`) for NuGet compliance
- Release automation workflows for CI and NuGet publish
- Public API XML documentation coverage with CI enforcement (`CS1591`)
- NuGet metadata improvements (repository/project URLs, package readme integration)

### Changed
- README focused on generated-parser usage model and reduced low-level API emphasis

## [0.1.0] - 2026-02-18

### Added
- Initial .NET runtime library structure for parser and scanner support
