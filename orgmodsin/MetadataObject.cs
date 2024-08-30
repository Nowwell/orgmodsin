using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace orgmodsin
{
    public class MetadataObject
    {
        public string directoryName { get; set; } = "";
        public bool inFolder { get; set; } = false;
        public bool metaFile { get; set; } = false;
        public string suffix { get; set; } = "";
        public string xmlName { get; set; } = "";
        public string[]? childXmlNames { get; set; }

        public override string ToString()
        {
            string childNames = "";
            if(childXmlNames != null && childXmlNames.Length > 0)
            {
                childNames = string.Join(";", childXmlNames);
            }

            return string.Format("{0},{1},{2},{3},{4},\"{5}\"", xmlName, directoryName, suffix, inFolder, metaFile, childNames);
        }
    }
}
