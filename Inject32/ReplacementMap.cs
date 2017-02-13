using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Inject
{
    public class ReplacementString
    {
        [XmlAttribute]
        public string Find { get; set; }
        [XmlAttribute]
        public string ReplaceWith { get; set; }
    }

    public class ReplacementMap
    {
        public static IReadOnlyCollection<ReplacementMap> Load()
        {
            try
            {
                var file = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.xml");
                if (!File.Exists(file))
                    return null;
                using (var stream = new MemoryStream(File.ReadAllBytes(file)))
                {
                    return (ReplacementMap[])new XmlSerializer(typeof(ReplacementMap[])).Deserialize(stream);
                }
            }
            catch
            {
                return null;
            }
        }

        [XmlAttribute]
        public string Branch { get; set; }
        public ReplacementString[] Replacements { get; set; }
    }
}
