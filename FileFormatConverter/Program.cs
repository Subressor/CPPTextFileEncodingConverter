using System;
using System.IO;
using System.Linq;
using System.Text;

namespace FileFormatConverter
{
    class Program
    {
        private enum BomType
        {
            None,
            Utf8,
            Utf16LE,
            Utf16BE,
            Utf32LE,
            Utf32BE
        }

        static void Main(string[] args)
        {
            // --- Welcome Message ---
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("======================================");
            Console.WriteLine("  File Format Standardization Utility ");
            Console.WriteLine("======================================");
            Console.ResetColor();
            Console.WriteLine("This tool will convert all .h, .cpp, and .cs files to:");
            Console.WriteLine("- Encoding: UTF-8 (without BOM)");
            Console.WriteLine("- Line Endings: Windows (CRLF)");
            Console.WriteLine();

            // --- Argument Handling ---
            if (args.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: No folder path provided.");
                Console.ResetColor();
                Console.WriteLine("Usage: Drag and drop a folder onto the .exe, or run from the command line:");
                Console.WriteLine(@"Example: FileFormatConverter.exe ""C:\MyUnrealProject\Source""");
                Console.ReadKey();
                return;
            }

            string targetDirectory = args[0];
            if (!Directory.Exists(targetDirectory))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: The specified directory does not exist: {targetDirectory}");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            // --- File Processing ---
            try
            {
                var targetExtensions = new[] { ".h", ".hpp", ".cpp", ".cc", ".cxx", ".cs" };
                var excludedDirMarkers = new[]
                {
                    Path.DirectorySeparatorChar + "Intermediate" + Path.DirectorySeparatorChar,
                    Path.DirectorySeparatorChar + "Binaries" + Path.DirectorySeparatorChar,
                    Path.DirectorySeparatorChar + "Saved" + Path.DirectorySeparatorChar,
                    Path.DirectorySeparatorChar + "DerivedDataCache" + Path.DirectorySeparatorChar,
                    Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar,
                    Path.DirectorySeparatorChar + ".vs" + Path.DirectorySeparatorChar
                };

                var filesToProcess = Directory.EnumerateFiles(targetDirectory, "*.*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f);
                        if (string.IsNullOrEmpty(ext)) return false;
                        if (!targetExtensions.Contains(ext.ToLowerInvariant())) return false;
                        // Skip common generated/temporary/VCS dirs
                        foreach (var marker in excludedDirMarkers)
                        {
                            if (f.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                                return false;
                        }
                        return true;
                    })
                    .ToList();

                if (filesToProcess.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("No matching files (.h, .hpp, .cpp, .cc, .cxx, .cs) were found in the specified directory (excluding Intermediate, Binaries, Saved, DerivedDataCache, .git, .vs).");
                    Console.ResetColor();
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"Found {filesToProcess.Count} files to process. Starting conversion...");
                Console.WriteLine();

                var targetEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); // UTF-8 without BOM

                int convertedCount = 0;
                int alreadyStandardCount = 0;
                int skippedBinaryCount = 0;
                int skippedReadOnlyCount = 0;
                int errorCount = 0;

                foreach (var filePath in filesToProcess)
                {
                    try
                    {
                        var attrs = File.GetAttributes(filePath);
                        if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine($"  -> Skipped (read-only): {filePath}");
                            Console.ResetColor();
                            skippedReadOnlyCount++;
                            continue;
                        }

                        // Sample a small prefix for BOM detection and binary heuristics
                        byte[] sample = ReadSample(filePath, 8192);
                        var bom = DetectBom(sample);
                        bool looksBinary = LooksBinaryWithoutTextBom(sample, bom);
                        if (looksBinary)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine($"  -> Skipped (binary-like): {filePath}");
                            Console.ResetColor();
                            skippedBinaryCount++;
                            continue;
                        }

                        // Determine if line endings need normalization (works for any BOM that StreamReader detects)
                        bool needsLineEndingFix = HasNonCRLFLineEndings(filePath);

                        // Any BOM present means we need to rewrite to UTF-8 without BOM
                        bool hasAnyBom = bom != BomType.None;

                        if (!hasAnyBom && !needsLineEndingFix)
                        {
                            Console.WriteLine($"  -> Already standard: {filePath}");
                            alreadyStandardCount++;
                            continue;
                        }

                        // Decode to text with BOM auto-detection, then rewrite with UTF-8 no BOM and CRLF
                        string[] lines = File.ReadAllLines(filePath, DetectReaderEncoding(bom));

                        WriteAllLinesWithCrlf(filePath, lines, targetEncoding);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  -> Standardized: {filePath}");
                        Console.ResetColor();
                        convertedCount++;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($"  -> Skipped (access denied): {filePath}");
                        Console.ResetColor();
                        skippedReadOnlyCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  -> Error: {filePath}");
                        Console.WriteLine($"     {ex.Message}");
                        Console.ResetColor();
                        errorCount++;
                    }
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Done.");
                Console.ResetColor();
                Console.WriteLine($"Converted: {convertedCount}");
                Console.WriteLine($"Already standard: {alreadyStandardCount}");
                Console.WriteLine($"Skipped (binary-like): {skippedBinaryCount}");
                Console.WriteLine($"Skipped (read-only/access denied): {skippedReadOnlyCount}");
                Console.WriteLine($"Errors: {errorCount}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("An unexpected error occurred:");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        // Reads up to maxBytes from the start of the file (or entire file if smaller).
        private static byte[] ReadSample(string filePath, int maxBytes)
        {
            using (var fs = File.OpenRead(filePath))
            {
                int toRead = (int)Math.Min(maxBytes, fs.Length);
                byte[] buffer = new byte[toRead];
                int read = 0;
                while (read < toRead)
                {
                    int n = fs.Read(buffer, read, toRead - read);
                    if (n <= 0) break;
                    read += n;
                }
                if (read != buffer.Length)
                {
                    Array.Resize(ref buffer, read);
                }
                return buffer;
            }
        }

        private static BomType DetectBom(byte[] bytes)
        {
            if (bytes.Length >= 4)
            {
                // UTF-32 BE: 00 00 FE FF
                if (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF) return BomType.Utf32BE;
                // UTF-32 LE: FF FE 00 00
                if (bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00) return BomType.Utf32LE;
            }
            if (bytes.Length >= 3)
            {
                // UTF-8: EF BB BF
                if (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return BomType.Utf8;
            }
            if (bytes.Length >= 2)
            {
                // UTF-16 BE: FE FF
                if (bytes[0] == 0xFE && bytes[1] == 0xFF) return BomType.Utf16BE;
                // UTF-16 LE: FF FE
                if (bytes[0] == 0xFF && bytes[1] == 0xFE) return BomType.Utf16LE;
            }
            return BomType.None;
        }

        // Treat files with UTF-16/32 BOM as text even if they contain zeros; otherwise, zero bytes suggest binary.
        private static bool LooksBinaryWithoutTextBom(byte[] sample, BomType bom)
        {
            if (bom == BomType.Utf16LE || bom == BomType.Utf16BE || bom == BomType.Utf32LE || bom == BomType.Utf32BE)
                return false;

            int len = sample.Length;
            int scan = Math.Min(len, 8192);
            for (int i = 0; i < scan; i++)
            {
                if (sample[i] == 0)
                    return true;
            }
            return false;
        }

        // Detects any non-CRLF line endings (LF-only or CR-only, or mixed) by analyzing decoded text.
        private static bool HasNonCRLFLineEndings(string filePath)
        {
            // Let StreamReader auto-detect BOM; else use UTF-8.
            using (var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true))
            {
                string content = reader.ReadToEnd();
                bool hasNonCrlf = false;
                for (int i = 0; i < content.Length; i++)
                {
                    char c = content[i];
                    if (c == '\r')
                    {
                        // If next is '\n', this is CRLF; skip the '\n'
                        if (i + 1 < content.Length && content[i + 1] == '\n')
                        {
                            i++; // skip '\n'
                        }
                        else
                        {
                            // CR without LF
                            hasNonCrlf = true;
                            break;
                        }
                    }
                    else if (c == '\n')
                    {
                        // LF not preceded by CR (LF-only)
                        hasNonCrlf = true;
                        break;
                    }
                }
                return hasNonCrlf;
            }
        }

        // Ensure CRLF explicitly and UTF-8 without BOM.
        private static void WriteAllLinesWithCrlf(string filePath, string[] lines, Encoding encoding)
        {
            using (var writer = new StreamWriter(filePath, append: false, encoding: encoding))
            {
                writer.NewLine = "\r\n";
                for (int i = 0; i < lines.Length; i++)
                {
                    writer.WriteLine(lines[i]);
                }
            }
        }

        // Provides an explicit encoding hint for ReadAllLines when a BOM exists; otherwise defaults to UTF-8.
        private static Encoding DetectReaderEncoding(BomType bom)
        {
            switch (bom)
            {
                case BomType.Utf8:
                    return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                case BomType.Utf16LE:
                    return new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
                case BomType.Utf16BE:
                    return new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
                case BomType.Utf32LE:
                    return new UTF32Encoding(bigEndian: false, byteOrderMark: true);
                case BomType.Utf32BE:
                    return new UTF32Encoding(bigEndian: true, byteOrderMark: true);
                default:
                    // No BOM: assume UTF-8 (typical for UE code and common repos)
                    return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            }
        }

        // Legacy helper kept for reference; no longer used directly.
        private static bool IsBinaryFile(string filePath)
        {
            byte[] buffer = new byte[8192];
            using (FileStream fs = File.OpenRead(filePath))
            {
                int bytesRead = fs.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == 0)
                        return true;
                }
            }
            return false;
        }
    }
}
