using System.Xml;

namespace orgmodsin
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string chosenUser = Authentication.DefaultUser();
            bool needsToAuth = false;
            string includes = "";
            string excludes = "";
            double api = 61;
            string ms = "";
            bool list = false;
            bool istest = false;

            string clientid = "";
            string clientsecret = "";

            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "--user" || args[i] == "-u") && i + 1 < args.Length)
                {
                    i++;
                    if (!Authentication.DoesUserExist(args[i]))
                    {
                        needsToAuth = true;
                    }
                    chosenUser = args[i];
                }
                if ((args[i] == "--alias" || args[i] == "-a") && i + 1 < args.Length)
                {
                    needsToAuth = true;
                    chosenUser = args[++i];
                }
                if ((args[i] == "--clientid" || args[i] == "-a") && i + 1 < args.Length)
                {
                    needsToAuth = true;
                    clientid = args[++i];
                }
                if ((args[i] == "--clientsecret" || args[i] == "-a") && i + 1 < args.Length)
                {
                    needsToAuth = true;
                    clientsecret = args[++i];
                }
                if ((args[i] == "--include" || args[i] == "-i") && i + 1 < args.Length)
                {
                    includes = args[++i];
                }
                if ((args[i] == "--exclude" || args[i] == "-e") && i + 1 < args.Length)
                {
                    excludes = args[++i];
                }
                if (args[i] == "--api" && i + 1 < args.Length)
                {
                    api = double.Parse(args[++i]);
                }
                if (args[i] == "--ms" && i + 1 < args.Length)
                {
                    ms = args[++i];
                }
                if (args[i] == "--list")
                {
                    list = true;
                }
                if (args[i] == "--sandbox")
                {
                    istest = true;
                }

            }

            DateTime modifiedSince = DateTime.Now;

            if (list == false && needsToAuth == false)
            {
                if (string.IsNullOrEmpty(ms))
                {
                    Console.WriteLine("the --ms (Modified since) flag is required");
                    return;
                }

                if (!DateTime.TryParse(ms, out modifiedSince))
                {
                    Console.WriteLine("Modified since datetime is invalid");
                    return;
                }
            }

            TokenResponse? token = null;
            if (needsToAuth == true)
            {
                token = await Authentication.Authenticate(chosenUser, clientid, clientsecret, istest);
                if (token == null)
                {
                    Console.WriteLine("Authentication failure.");
                    return;
                }

                Console.WriteLine("Authentication complete. Please run again with desired parameters");
                return;
            }
            else
            {
                if (string.IsNullOrEmpty(chosenUser.Trim()))
                {
                    Console.WriteLine("The Default user is not authenticated, or does not exist.");
                    return;
                }
                else
                {
                    token = Authentication.LoadUser(chosenUser.Trim());
                    if (token == null)
                    {
                        Console.WriteLine($"The {chosenUser} is not authenticated, or does not exist.");
                        return;
                    }
                }
            }

            int apiCalls = 0;

            //Metadata types to include, exclude
            // If includeList exists, then use that for includes.
            // If excludeList exists, then call org for metadata types and then filter out the excludes.
            // If includes and excludes exist, use the include list.
            string[]? includeList = null;
            if (!string.IsNullOrEmpty(includes.Trim()))
            {
                includeList = (includes.Contains(",") ? includes.Split(",") : new string[] { includes });
            }

            string[]? excludeList = null;
            if (!string.IsNullOrEmpty(excludes.Trim()))
            {
                excludeList = (excludes.Contains(",") ? excludes.Split(",") : new string[] { excludes });
            }

            using HttpClient client = new HttpClient();
            List<MetadataObject> mdos;

            string metadata = await Soap.DescribeMetadata(client, token, api, modifiedSince); apiCalls++;

            if (metadata.Contains("Session not found"))
            {
                Console.WriteLine("Authentication token is expired, please reauthenticate");
                token = await Authentication.Authenticate(chosenUser, token, istest);
                if (token == null)
                {
                    Console.WriteLine("Authentication failure.");
                    return;
                }

                metadata = await Soap.DescribeMetadata(client, token, api, modifiedSince); apiCalls++;
            }

            mdos = ParseMetadataObjects(metadata);

            if(list == true)
            {
                using (StreamWriter output = new StreamWriter("types.csv"))
                {
                    output.WriteLine("xmlName, directoryName, suffix, inFolder, metaFile, childXmlNames");
                    foreach (MetadataObject result in mdos)
                    {
                        output.WriteLine(result.ToString());
                    }
                    output.Flush();
                }

                Console.Write("Api Calls Used: {0}", apiCalls);

                return;
            }

            Queue<MetadataQuery> query = CreateQueries(includeList, excludeList, mdos);

            List<MetadataResult> results = new List<MetadataResult>();
            while (query.Count > 0)
            {
                MetadataQuery[] qq = new MetadataQuery[query.Count < 3 ? query.Count : 3];

                if(query.Count < 3)
                {
                    qq = query.ToArray();
                    query.Clear();
                }
                else
                {
                    qq[0] = query.Dequeue();
                    qq[1] = query.Dequeue();
                    qq[2] = query.Dequeue();
                }

                metadata = await Soap.ListMetadata(client, token, qq, api, modifiedSince); apiCalls++;

                List<MetadataResult> intermediate = ParseListMetadataResults(metadata);

                foreach(MetadataResult result in intermediate)
                {
                    if(result.type.EndsWith("Folder"))
                    {
                        MetadataQuery q = new MetadataQuery();
                        q.type = result.type.Replace("Folder", "");
                        if (q.type == "Email") q.type = "EmailTemplate";
                        q.folder = result.fullName;
                        query.Enqueue(q);
                    }
                }

                results.AddRange(intermediate);
            }

            using (StreamWriter output = new StreamWriter("changes.csv"))
            {
                output.WriteLine("id, type, fileName, fullName, createdById, createdByName, createdDate, lastModifiedById, lastModifiedByName, lastModifiedDate, namespacePrefix");
                foreach (MetadataResult result in results)
                {
                    if (DateTime.Parse(result.lastModifiedDate) > modifiedSince)
                    {
                        output.WriteLine(result.ToString());
                    }
                }
                output.Flush();
            }
            Console.Write("Api Calls Used: {0}", apiCalls);
        }

        static List<MetadataObject> ParseMetadataObjects(string metadata)
        {
            List<MetadataObject> retVal = new List<MetadataObject>();

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(metadata);

            XmlNodeList parentTypes = xmlDocument.GetElementsByTagName("metadataObjects");

            foreach (XmlNode node in parentTypes)
            {
                MetadataObject mo = new MetadataObject();
                List<string> childXmlNames = new List<string>();

                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name == "directoryName")
                        mo.directoryName = child.InnerText;
                    if (child.Name == "suffix")
                        mo.suffix = child.InnerText;
                    if (child.Name == "xmlName")
                        mo.xmlName = child.InnerText;
                    if (child.Name == "inFolder")
                        mo.inFolder = bool.Parse(child.InnerText);
                    if (child.Name == "metaFile")
                        mo.metaFile = bool.Parse(child.InnerText);
                    if (child.Name == "childXmlNames")
                        childXmlNames.Add(child.InnerText);
                }

                mo.childXmlNames = childXmlNames.ToArray();

                retVal.Add(mo);
            }

            return retVal;
        }

        static List<MetadataResult> ParseListMetadataResults(string metadata)
        {
            List<MetadataResult> retVal = new List<MetadataResult>();

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(metadata);

            XmlNodeList parentTypes = xmlDocument.GetElementsByTagName("result");

            foreach (XmlNode node in parentTypes)
            {
                MetadataResult mo = new MetadataResult();

                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name == "createdById")
                        mo.createdById = child.InnerText;
                    if (child.Name == "createdByName")
                        mo.createdByName = child.InnerText;
                    if (child.Name == "createdDate")
                        mo.createdDate = child.InnerText;
                    if (child.Name == "fileName")
                        mo.fileName = child.InnerText;
                    if (child.Name == "fullName")
                        mo.fullName = child.InnerText;
                    if (child.Name == "id")
                        mo.id = child.InnerText;
                    if (child.Name == "lastModifiedById")
                        mo.lastModifiedById = child.InnerText;
                    if (child.Name == "lastModifiedByName")
                        mo.lastModifiedByName = child.InnerText;
                    if (child.Name == "lastModifiedDate")
                        mo.lastModifiedDate = child.InnerText;
                    if (child.Name == "namespacePrefix")
                        mo.namespacePrefix = child.InnerText;
                    if (child.Name == "type")
                        mo.type = child.InnerText;
                }

                retVal.Add(mo);
            }

            return retVal;
        }

        static Queue<MetadataQuery> CreateQueries(string[]? includeList,  string[]? excludeList, List<MetadataObject> mdos)
        {
            Queue<MetadataQuery> query = new Queue<MetadataQuery>();
            if (includeList != null)
            {
                foreach (MetadataObject dmo in mdos)
                {
                    if (includeList.Contains(dmo.xmlName))
                    {
                        MetadataQuery q = new MetadataQuery();
                        q.type = dmo.xmlName + (dmo.inFolder ? "Folder" : "");
                        q.folder = (dmo.inFolder ? "*" : "");
                        if (q.type == "EmailTemplateFolder")
                            q.type = "EmailFolder";

                        query.Enqueue(q);
                    }

                    if (dmo.childXmlNames != null)
                    {
                        foreach (string child in dmo.childXmlNames)
                        {
                            if (includeList.Contains(child))
                            {
                                MetadataQuery q = new MetadataQuery();
                                q.type = child;

                                query.Enqueue(q);
                            }
                        }
                    }
                }
            }
            else if (excludeList != null)
            {
                foreach (MetadataObject dmo in mdos)
                {
                    if (!excludeList.Contains(dmo.xmlName))
                    {
                        MetadataQuery q = new MetadataQuery();
                        q.type = dmo.xmlName + (dmo.inFolder ? "Folder" : "");
                        q.folder = (dmo.inFolder ? "*" : "");
                        if (q.type == "EmailTemplateFolder")
                            q.type = "EmailFolder";

                        query.Enqueue(q);
                    }

                    if (dmo.childXmlNames != null)
                    {
                        foreach (string child in dmo.childXmlNames)
                        {
                            if (!excludeList.Contains(child))
                            {
                                MetadataQuery q = new MetadataQuery();
                                q.type = child;

                                query.Enqueue(q);
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (MetadataObject dmo in mdos)
                {
                    MetadataQuery q = new MetadataQuery();
                    q.type = dmo.xmlName + (dmo.inFolder ? "Folder" : "");
                    q.folder = (dmo.inFolder ? "*" : "");
                    if (q.type == "EmailTemplateFolder")
                        q.type = "EmailFolder";

                    query.Enqueue(q);

                    if (dmo.childXmlNames != null)
                    {
                        foreach (string child in dmo.childXmlNames)
                        {
                            MetadataQuery q2 = new MetadataQuery();
                            q2.type = child;

                            query.Enqueue(q2);
                        }
                    }
                }
            }

            return query;
        }
    }
}