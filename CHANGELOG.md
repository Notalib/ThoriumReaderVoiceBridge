# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Changed

- The app is now distributed as a signed MSI installer with a Start Menu shortcut,
  replacing the manually copied script folder.
- Elevation now happens through a single UAC prompt on the shortcut instead of the
  previous `.cmd` → `.vbs` → PowerShell chain.

## [1.3.0]

- Last version distributed as a loose script folder.
