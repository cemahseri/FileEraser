using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileEraser;

internal static class Program
{
    // Streams' default buffer size is 4096 bytes. That is not really useful for reading big files.
    // So, instead of 4 KBs, using 1 MB as the buffer size will be better.
    // I was using 4 MBs before but thanks to Dai, I've changed it to 1 MB: https://stackoverflow.com/questions/1862982/c-sharp-filestream-optimal-buffer-size-for-writing-large-files#comment123344066_1863003
    private const int BufferSize = 1 * 1024 * 1024;

    // We are allocating an array with 1,048,576 (1 * 1024 * 1024) items. Array's type is byte, so each item will take 1 byte.
    // So, array will take 1 MB in the memory. This buffer is for overwriting the files. Writing byte by byte is so slow, so we are slowing the whole array at a time.
    private static readonly byte[] Buffer = new byte[BufferSize];

    // This is for verification process. We could create an array as a local variable but allocating memory and Garbage Collecting every time will affect the performance.
    // So, create an array once, always change its content and never allow GC to run.
    private static readonly byte[] TemporaryBuffer = new byte[BufferSize];

    private static async Task Main(string[] paths)
    {
        if (await SupportTrim())
        {
            Console.WriteLine("You have TRIM enabled. It's impossible to recover files that's have been deleted in an SSD which as TRIM enabled.");
            Console.WriteLine("You can delete your files seceurely without any additional software. Press any key to exit...");

            Console.ReadKey(true);
            return;
        }

        if (paths.Length < 1)
        {
            Console.WriteLine("No path(s) specified. Press any key to exit...");

            Console.ReadKey(true);
            return;
        }
        
        var deletedFiles = 0;
        var deletedDirectories = 0;

        foreach (var path in paths)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine("Path does not exist. Skipping: " + path);
                Console.WriteLine();
                continue;
            }

            var mainDirectory = new DirectoryInfo(path);

            // "*.*" will only get files with extensions. But "*" will get every file.
            // And if SearchOption.AllDirectories wasn't used, it wouldn't be recursive scan and it'd only get the files in the target path, with not including files in the child directories.
            var files = mainDirectory.GetFiles("*", SearchOption.AllDirectories);

            // If there are no files to compare, skip that folder.
            if (!files.Any())
            {
                continue;
            }
            
            Console.WriteLine($"Files to delete: {files.Length} ({path})");

            // OrderBy method is not really necessary here. Just sorting files based on their sizes. It means that smallest file will get deleted first.
            foreach (var file in files.OrderBy(f => f.Length))
            {
                // Removing other attributes, if present, to delete the file without any errors.
                File.SetAttributes(file.FullName, FileAttributes.Normal);

                var shouldDelete = true;

                // If file's size is equals to 0, it means that there is no byte to overwrite. So, skip this part and directly delete it.
                if (file.Length != 0)
                {
                    // We have to lock the file with FileShare.None, because any other write operation shouldn't be allowed. It may interrupt our writing and verifying process.
                    await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None, BufferSize, FileOptions.Asynchronous);

                    if (!stream.CanRead)
                    {
                        Console.WriteLine("Stream doesn't support reading. Skipping deleting file: " + file.FullName);
                        continue;
                    }

                    if (!stream.CanWrite)
                    {
                        Console.WriteLine("Stream doesn't support writing. Skipping deleting file: " + file.FullName);
                        continue;
                    }

                    await Overwrite(stream).ConfigureAwait(false);

                    var isFullOfZeros = await IsFullOfZeros(stream);
                    if (!isFullOfZeros)
                    {
                        Console.WriteLine("Verifying failed. File's content isn't full of 0x00 bytes. Skipping deleting file: " + file.FullName);

                        shouldDelete = false;
                    }
                }

                if (shouldDelete)
                {
                    file.Delete();
                    
                    deletedFiles++;
                }
            }

            // Excluding folders with files or any other folders, because directories must be empty to be get deleted.
            var directories = mainDirectory.GetDirectories("*", SearchOption.AllDirectories).Where(d => d.GetFiles().Length == 0 && d.GetDirectories().Length == 0).ToList();

            foreach (var directory in directories)
            {
                // Again, if you don't set directory's attributes to FileAttributes.Normal, you cannot delete the directory successfully. It can occur problems sometimes.
                // You might think "Hey! You are using File.SetAttribute! But a folder ain't file! WTF?!" Well, a folder is basically a file, with FileAttributes.Directory attribute.
                File.SetAttributes(directory.FullName, FileAttributes.Normal);
                directory.Delete();
                
                deletedDirectories++;
            }

            // Also deleting the main directory.
            File.SetAttributes(mainDirectory.FullName, FileAttributes.Normal);
            mainDirectory.Delete();

            deletedDirectories++;

            Console.WriteLine("Directory deleted securely.");
            Console.WriteLine();
        }

        Console.WriteLine($"{deletedDirectories} directories and {deletedFiles} files have been deleted safely. Press any key to exit...");
        Console.ReadKey(true);
    }

    private static async Task Overwrite(Stream stream)
    {
        var numberOfChunks = stream.Length / BufferSize;

        // First, writing as chunks.
        for (var i = 0; i < numberOfChunks; i++)
        {
            await stream.WriteAsync(Buffer.AsMemory(0, BufferSize));
        }

        // Then overwriting the remaining bytes.
        if (stream.Position != stream.Length)
        {
            await stream.WriteAsync(Buffer.AsMemory(0, (int)(stream.Length % BufferSize)));
        }

        // Set back stream's position to the beginning, for the verifying process.
        stream.Position = 0;
    }

    private static async Task<bool> IsFullOfZeros(Stream stream)
    {
        var numberOfChunks = stream.Length / BufferSize;

        // Verifying overwritten bytes.
        var remainingBytes = (int)stream.Length;

        // First, reading as chunks.
        for (var chunk = 0; chunk < numberOfChunks; chunk++)
        {
            remainingBytes -= await stream.ReadAsync(TemporaryBuffer.AsMemory(0, BufferSize));

            for (var i = 0; i < BufferSize; i++)
            {
                if (TemporaryBuffer[i] != 0x00)
                {
                    return false;
                }
            }
        }

        // Then reading the remaining bytes.
        while (remainingBytes > 0)
        {
            remainingBytes -= await stream.ReadAsync(TemporaryBuffer.AsMemory(0, remainingBytes));

            for (var i = 0; i < remainingBytes; i++)
            {
                if (TemporaryBuffer[i] != 0x00)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static async Task<bool> SupportTrim()
    {
        var process = new Process
        {
            StartInfo =
            {
                FileName = "fsutil",
                Arguments = "behavior query DisableDeleteNotify",

                RedirectStandardOutput = true,

                UseShellExecute = false,
                CreateNoWindow = true, // For hiding the window that's going to be created.
                WindowStyle = ProcessWindowStyle.Hidden, // For hiding the window that's going to be created.
            }
        };

        process.Start();

        while (!process.StandardOutput.EndOfStream)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            // 0 means TRIM enabled, 1 means disabled
            if (line.Contains("DisableDeleteNotify = 1"))
            {
                return false;
            }
        }

        return true;
    }
}