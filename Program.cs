using System;
using System.Collections.Generic;
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

            var directory = new DirectoryInfo(args[0]);

            // "*.*" will only get files with extensions. But "*" will get every file.
            // And if SearchOption.AllDirectories wasn't used, it wouldn't be recursive scan and it'd only get the files in the target path, with not including files in the child directories.
            Console.WriteLine("Getting files...");
            var files = directory.GetFiles("*", SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                Console.WriteLine("Uhhh... Dude, this directory is empty.");
                Console.ReadKey();
                return;
            }

            Console.ReadKey();
        }
    }
}