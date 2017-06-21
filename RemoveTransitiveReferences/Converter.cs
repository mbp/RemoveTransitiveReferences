using System;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace RemoveTransitiveReferences
{
    public class Converter
    {
        public void Convert(string file)
        {
            Console.WriteLine($"Reading file {file}");
            XDocument xmlDocument;
            using (var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                xmlDocument = XDocument.Load(stream);
            }

            var parser = new Parser();
            xmlDocument = parser.Parse(xmlDocument, file);
            if (xmlDocument != null)
            {
                Console.WriteLine($"Writing file {file}");
                File.WriteAllText(file, xmlDocument.ToString(), Encoding.UTF8);
            }
        }
    }
}