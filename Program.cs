using System;
using System.IO;
using System.Linq;

namespace FileEraser
{
    internal static class Program
    {
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

                // If file's size is equals to 0, it means that there is no bytes to overwrite. So, skip this part and directly delete it.
                if (file.Length != 0)
                {
                    // We have to lock the file with FileShare.None, because any other write operation shouldn't be allowed. It may interrupt our writing and verifying process.
                    // And using 4 MBs as the buffer size, because it'll help reading big files. (P.S. Default buffer size is 4 KBs.)
                    using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4 * 1024 * 1024);

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

                    // Might refactor this part later and combine those two methods in a single method.
                    PerformFirstTwoPasses(stream);
                    PerformThirdPass(stream);
                }

                file.Delete();
                Console.WriteLine("File deleted: " + file.FullName);
            }

            var directories = mainDirectory.GetDirectories("*", SearchOption.AllDirectories);

            foreach (var directory in directories)
            {
                // Again, if you don't set directory's attributes to FileAttributes.Normal, you cannot delete the directory successfully. It can occur problems sometimes.
                // And don't think like "Hey! You are using File.SetAttribute! But a folder ain't file! WTF?!" Because a folder is basically a file, with FileAttributes.Directory attribute.
                File.SetAttributes(directory.FullName, FileAttributes.Normal);

                directory.Delete();
                Console.WriteLine("Directory deleted: " + directory.FullName);
            }

            // Also deleting the main directory.
            File.SetAttributes(mainDirectory.FullName, FileAttributes.Normal);
            mainDirectory.Delete();
            Console.WriteLine("Directory deleted: " + mainDirectory.FullName + Environment.NewLine);

            Console.WriteLine(directories.Length + " directories and " + files.Length + " files have been deleted safely."); 
            Console.ReadKey();
        }

        private static void PerformFirstTwoPasses(Stream stream)
        {
            // I've used @ prefix while naming the variable, because byte is a keyword that C# uses. So, if you would like to use a keyword that also C# uses, just use @ prefix.
            // Wrong:   var  string = "Hey!";
            // Correct: var @string = "Hey!";
            foreach (var @byte in new byte[]{ 0x00, 0xFF })
            {
                while (stream.Position < stream.Length)
                {
                    stream.WriteByte(@byte);
                }

                // Set back stream's position to the beginning.
                stream.Seek(0, SeekOrigin.Begin);

                // Now flushing modified stream to the file to overwrite the content.
                stream.Flush();

                // Verifying overwritten bytes.
                while (stream.Position < stream.Length)
                {
                    var readValue = stream.ReadByte();
                    if (readValue == -1) // -1 means that we are at the end of the stream. So, break the while loop, which is reading the stream.
                    {
                        break;
                    }

                    if (readValue != @byte)
                    {
                        throw new Exception($"{@byte:X2} byte is expected but instead received {readValue:X2}. Verifying failed. Aborting deleting process.");
                    }
                }

                stream.Seek(0, SeekOrigin.Begin);
            }
        }

        private static void PerformThirdPass(Stream stream)
        {
            var random = new Random();

            while (stream.Position < stream.Length)
            {
                stream.WriteByte((byte)random.Next(0, 256));
            }

            // Currently, I am not verifying the third pass. I'm not sure if we even have to verify the third pass or not.
            // Anyways, might add that later.

            stream.Flush();
        }
    }
}