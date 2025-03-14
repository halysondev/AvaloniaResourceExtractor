using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AvaloniaResourceExtractor
{
    class AvaloniaResourcesIndexEntry
    {
        public byte PathSize { get; set; }
        public string Path { get; set; } = "";
        public int Offset { get; set; }
        public int Size { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string resourceFileName = "-AvaloniaResources";
            string resourcePath = Path.Combine(appDirectory, resourceFileName);

            if (!File.Exists(resourcePath))
            {
                Console.WriteLine($"Resource file not found: {resourcePath}");
                return;
            }

            using FileStream resourceStream = File.OpenRead(resourcePath);
            using BinaryReader reader = new BinaryReader(resourceStream);
            long totalLength = resourceStream.Length;

            // 1) Read indexLength, version, entryCount
            if (resourceStream.Position + 12 > totalLength)
            {
                Console.WriteLine("Not enough data to read the 3 header ints (indexLength, version, entryCount).");
                return;
            }

            int indexLength = reader.ReadInt32();
            int version = reader.ReadInt32();
            int entryCount = reader.ReadInt32();

            Console.WriteLine($"indexLength={indexLength}, version={version}, entryCount={entryCount}");

            // 2) Read the array of index entries
            List<AvaloniaResourcesIndexEntry> entries = new List<AvaloniaResourcesIndexEntry>(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                // Each entry: [uint8 PathSize], [PathSize bytes Path], [int offset], [int size]
                if (resourceStream.Position + 1 > totalLength)
                {
                    Console.WriteLine($"Not enough data to read PathSize for entry {i + 1}.");
                    return;
                }
                byte pathSize = reader.ReadByte();

                if (resourceStream.Position + pathSize + 8 > totalLength)
                {
                    Console.WriteLine($"Not enough data to read Path + offset + size for entry {i + 1}.");
                    return;
                }

                // Read the path bytes
                string pathStr = Encoding.UTF8.GetString(reader.ReadBytes(pathSize));

                // Read offset and size (each 4 bytes)
                int offsetVal = reader.ReadInt32();
                int sizeVal = reader.ReadInt32();

                entries.Add(new AvaloniaResourcesIndexEntry
                {
                    PathSize = pathSize,
                    Path = pathStr,
                    Offset = offsetVal,
                    Size = sizeVal
                });
            }

            // 3) The baseOffset is indexLength + 4 (per the template)
            int baseOffset = indexLength + 4;
            Console.WriteLine($"baseOffset = {baseOffset}");

            if (baseOffset < 0 || baseOffset > totalLength)
            {
                Console.WriteLine($"baseOffset={baseOffset} is out of file range. totalLength={totalLength}");
                return;
            }

            // 4) Extract each resource
            foreach (var entry in entries)
            {
                long absoluteOffset = baseOffset + entry.Offset;
                if (absoluteOffset < 0 || (absoluteOffset + entry.Size) > totalLength)
                {
                    Console.WriteLine($"Invalid offset/size for asset '{entry.Path}'. offset={entry.Offset}, size={entry.Size}");
                    continue;
                }

                resourceStream.Seek(absoluteOffset, SeekOrigin.Begin);
                byte[] assetData = reader.ReadBytes(entry.Size);

                // Remove any leading directory separators so the path is relative
                string relativePath = entry.Path.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string outputPath = Path.Combine(appDirectory, relativePath);

                // Ensure that the target directory exists
                string? outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                Console.WriteLine($"Extracting asset: '{relativePath}', AbsoluteOffset={absoluteOffset}, Size={entry.Size}");

                try
                {
                    File.WriteAllBytes(outputPath, assetData);
                    Console.WriteLine($"Saved asset to: {outputPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed writing asset '{relativePath}' to disk: {ex.Message}");
                }
            }

            Console.WriteLine("Extraction attempt finished.");
        }
    }
}
