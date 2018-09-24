using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace BlueMuse.Helpers
{
    public static class DataContractSerializerExtension
    {
        public static string Serialize<T>(this T obj)
        {
            var ms = new MemoryStream();
            // Write an object to the Stream and leave it opened
            using (var writer = XmlDictionaryWriter.CreateTextWriter(ms, Encoding.UTF8, ownsStream: false))
            {
                var ser = new DataContractSerializer(typeof(T));
                ser.WriteObject(writer, obj);
            }
            // Read serialized string from Stream and close it
            using (var reader = new StreamReader(ms, Encoding.UTF8))
            {
                ms.Position = 0;
                return reader.ReadToEnd();
            }
        }

        public static T Deserialize<T>(this string xml)
        {
            var ms = new MemoryStream();
            // Write xml content to the Stream and leave it opened
            using (var writer = new StreamWriter(ms, Encoding.UTF8, 512, leaveOpen: true))
            {
                writer.Write(xml);
                writer.Flush();
                ms.Position = 0;
            }
            // Read Stream to the Serializer and Deserialize and close it
            using (var reader = XmlDictionaryReader.CreateTextReader(ms, Encoding.UTF8, new XmlDictionaryReaderQuotas(), null))
            {
                var ser = new DataContractSerializer(typeof(T));
                return (T)ser.ReadObject(reader);
            }
        }
    }
}
