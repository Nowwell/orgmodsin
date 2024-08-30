using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace orgmodsin
{
    public class MetadataQuery
    {
        public string type { get; set; } = "";
        public string folder { get; set; } = "";

        public override string ToString()
        {
            return type;
        }
    }
}
