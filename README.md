# File Format Standardization Utility (UTF‑8 no BOM, CRLF)

A small Windows console tool that converts source files to:
- Encoding: UTF‑8 without BOM (recommended by Unreal Engine)
- Line endings: Windows CRLF (`\r\n`)

Useful for Unreal Engine projects, but works for any codebase needing consistent encoding and line endings.

## Features
- Converts .h, .hpp, .cpp, .cc, .cxx, .cs
- Writes UTF‑8 without BOM
- Normalizes line endings to CRLF
- Skips common generated/VCS folders: Intermediate, Binaries, Saved, DerivedDataCache, .git, .vs
- Skips read-only files and likely-binary files
- Clear per-file status and summary

## Safety
- No write if a file is already standard (preserves timestamps)
- Per-file error handling (one failure won’t stop the run)
- Heuristic to avoid mangling binary files
- Recommend running under source control (easy diffs/reverts)

## Requirements
- Windows
- .NET Framework 4.7.2
- Built with Visual Studio 2022

## Build
1. Open the solution in Visual Studio 2022
2. Set Configuration to Release
3. Build the solution
4. The EXE will be in `.\bin\Release\`

## Usage

### Drag-and-drop (simple)
- Build the app
- Drag a target folder (e.g., your Unreal `Source` folder) onto the EXE
- The tool will recursively process all matching files

### Command-line

### What gets processed
- Extensions: `.h`, `.hpp`, `.cpp`, `.cc`, `.cxx`, `.cs`
- Skipped directories: `Intermediate`, `Binaries`, `Saved`, `DerivedDataCache`, `.git`, `.vs`
- Skipped files: read-only, binary-like

## How it works
- Detects BOM (UTF‑8/16/32) and rewrites to UTF‑8 without BOM
- Detects LF-only, CR-only, and mixed endings; rewrites to CRLF explicitly
- Uses a small read-ahead to avoid touching likely binary files

## Known limitations
- Encoding detection beyond BOM is heuristic; rare encodings may fail
- Read-only files are reported but not modified

## License
Choose a license (MIT is common for small utilities). For example:
- MIT: https://opensource.org/licenses/MIT