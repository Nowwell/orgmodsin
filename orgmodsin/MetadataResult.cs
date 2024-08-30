using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace orgmodsin
{
    internal class MetadataResult
    {
        public string createdById { get; set; } = "";
        public string createdByName { get; set; } = "";
        public string createdDate { get; set; } = "";
        public string fileName { get; set; } = "";
        public string fullName { get; set; } = "";
        public string id { get; set; } = "";
        public string lastModifiedById { get; set; } = "";
        public string lastModifiedByName { get; set; } = "";
        public string lastModifiedDate { get; set; } = "";
        public string namespacePrefix { get; set; } = "";
        public string type { get; set; } = "";

        public override string ToString()
        {
            return string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}", id, type, fileName, fullName, createdById, createdByName, createdDate, lastModifiedById, lastModifiedByName, lastModifiedDate, namespacePrefix);
        }

    }
}
