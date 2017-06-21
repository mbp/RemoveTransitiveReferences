using System;
using System.IO;

namespace RemoveTransitiveReferences
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: [csproj] or directory containing csproj in subdirectories");
                return;
            }
            string file = args[0];

            if (Directory.Exists(file))
            {
                RunDirectory(file);
                return;
            }

            if (!File.Exists(file))
            {
                Console.WriteLine($"File or directory '{file}' does not exist");
                return;
            }

            RunFile(file);
        }

        private static void RunDirectory(string directory)
        {
            Console.WriteLine($"Reading directory {directory}");

            var files = Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                RunFile(file);
            }
        }

        private static void RunFile(string file)
        {
            try
            {
                var converter = new Converter();
                converter.Convert(file);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}