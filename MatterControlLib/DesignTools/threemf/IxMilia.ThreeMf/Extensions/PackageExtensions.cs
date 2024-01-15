using System;
using System.IO;
using System.IO.Packaging;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace IxMilia.ThreeMf.Extensions
{
    internal static class PackageExtensions
    {
        private static XmlWriterSettings WriterSettings = new XmlWriterSettings()
        {
            Encoding = Encoding.UTF8,
            Indent = true,
            IndentChars = "  "
        };

        public static byte[] GetPartBytes(this Package package, string uri)
        {
            var packagePart = package.GetPart(new Uri(uri, UriKind.RelativeOrAbsolute));
            using (var stream = packagePart.GetStream())
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                var data = new byte[memoryStream.Length];
                memoryStream.Read(data, 0, data.Length);
                return data;
            }
        }

        public static PackagePart WriteXml(this Package package, string path, string contentType, XElement xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException(nameof(xml));
            }

            var part = package.CreatePart(new Uri(path, UriKind.RelativeOrAbsolute), contentType, CompressionOption.Normal);
            using (var stream = part.GetStream())
            using (var writer = XmlWriter.Create(stream, WriterSettings))
            {
                var document = new XDocument(xml);
                document.WriteTo(writer);
            }

            return part;
        }

        public static PackagePart WriteBinary(this Package package, string path, string contentType, byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var part = package.CreatePart(new Uri(path, UriKind.RelativeOrAbsolute), contentType, CompressionOption.Normal);
            using (var stream = part.GetStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(data);
            }

            return part;
        }
    }
}
