using System;
using System.IO;
using System.Linq;

namespace FileEraser
{
    internal static class Program
    {
        // Streams' default buffer size is 4096 bytes. That is not really useful for reading or writing big files.
        // So, instead of 4 KBs, using 4 MBs as the buffer size will be better.
        private const int BufferSize = 4 * 1024 * 1024;

        // We are allocating an array with 4,194,304 (4 * 1024 * 1024) items. Array's type is byte, so each item will take 1 byte.
        // So, array will take 4 MBs in the memory. This buffer is for overwriting the files. Writing byte by byte is so slow.
        private static readonly byte[] Buffer = new byte[BufferSize];

        // This is for verification process. We could create an array as a local variable but allocating memory and Garbage Collecting every time will affect the performance.
        // So, create an array once, always change its content and never allow GC to run.
        private static readonly byte[] TemporaryBuffer = new byte[BufferSize];

        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                throw new ArgumentException("No path specified.");
            }

            var mainDirectory = new DirectoryInfo(args[0]);

            // "*.*" will only get files with extensions. But "*" will get every file.
            // And if SearchOption.AllDirectories wasn't used, it wouldn't be recursive scan and it'd only get the files in the target path, with not including files in the child directories.
            Console.WriteLine("Getting files...");
            var files = mainDirectory.GetFiles("*", SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                Console.WriteLine("Uhhh... Dude, this directory is empty.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Files to delete: " + files.Length + Environment.NewLine);
            
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
                    using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None, BufferSize);

                    if (!stream.CanRead)
                    {
                        throw new Exception("Stream doesn't support reading the file: " + file.FullName);
                    }

                    if (!stream.CanWrite)
                    {
                        throw new Exception("Stream doesn't support overwriting the file: " + file.FullName);
                    }

                    if (!stream.CanSeek)
                    {
                        throw new Exception("Stream doesn't support manually setting position of itself: " + file.FullName);
                    }

                    Overwrite(stream);

                    if (!IsFullOfZeros(stream))
                    {
                        Console.WriteLine("Verifying failed. File's content isn't full of 00 bytes. Skipping deleting file: " + file.FullName);

                        shouldDelete = false;
                    }
                }

                if (shouldDelete)
                {
                    file.Delete();
                    Console.WriteLine("File deleted: " + file.FullName);
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
                Console.WriteLine("Directory deleted: " + directory.FullName);
            }

            // Also deleting the main directory.
            File.SetAttributes(mainDirectory.FullName, FileAttributes.Normal);
            mainDirectory.Delete();
            Console.WriteLine("Directory deleted: " + mainDirectory.FullName + Environment.NewLine);

            Console.WriteLine(directories.Count + " directories and " + files.Length + " files have been deleted safely.");
            Console.ReadKey();
        }

        private static void Overwrite(Stream stream)
        {
            var numberOfChunks = stream.Length / BufferSize;

            // First, writing as chunks.
            for (var i = 0; i < numberOfChunks; i++)
            {
                stream.Write(Buffer, 0, BufferSize);
            }

            // Then overwriting the remaining bytes.
            if (stream.Position != stream.Length)
            {
                stream.Write(Buffer, 0, (int)(stream.Length % BufferSize));
            }

            // Set back stream's position to the beginning, for the verifying process.
            stream.Position = 0;
        }

        private static bool IsFullOfZeros(Stream stream)
        {
            var numberOfChunks = stream.Length / BufferSize;

            // Verifying overwritten bytes.
            var remainingBytes = (int)stream.Length;

            // First, reading as chunks.
            for (var chunk = 0; chunk < numberOfChunks; chunk++)
            {
                remainingBytes -= stream.Read(TemporaryBuffer, 0, BufferSize);

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
                remainingBytes -= stream.Read(TemporaryBuffer, 0, remainingBytes);

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
    }
}