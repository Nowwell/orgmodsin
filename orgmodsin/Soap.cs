using orgmodsin;
using System.Numerics;
using System.Text;
using System.Xml.Serialization;

namespace orgmodsin
{

    public class Soap
    {
        public async static Task<String> DescribeMetadata(HttpClient client, TokenResponse token, double version, DateTime modifiedSince)
        {
            string soapBody = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tns=""http://soap.sforce.com/2006/04/metadata"">
<soapenv:Header>
<tns:SessionHeader>
<tns:sessionId>{0}</tns:sessionId>
</tns:SessionHeader>
</soapenv:Header>
<soapenv:Body>
<tns:describeMetadata>
<asOfVersion>{1:F1}</asOfVersion>
</tns:describeMetadata>
</soapenv:Body>
</soapenv:Envelope>", token.access_token, version);

            try
            {

                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, token.instance_url + "/services/Soap/m/61.0"))
                {
                    soapBody = soapBody.Replace("\n", "").Replace("\r", "");
                    using (StringContent content = new StringContent(soapBody, Encoding.UTF8, "text/xml"))
                    {
                        request.Headers.Add("Accept", "text/xml");
                        request.Headers.Add("charset", "UTF-8");
                        request.Headers.Add("SOAPAction", "login");
                        request.Headers.Add("If-Modified-Since", modifiedSince.ToString("R"));//Wish it supported this... :(
                        request.Content = content;

                        using (HttpResponseMessage response = await client.SendAsync(request))
                        {
                            response.EnsureSuccessStatusCode();
                            string retValue = await response.Content.ReadAsStringAsync();

                            return retValue;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return "";
        }

        public async static Task<String> ListMetadata(HttpClient client, TokenResponse token, MetadataQuery[] mdo, double version, DateTime modifiedSince)
        {

            string queryTemplate = @"
<listMetadataQuery>
    <type>{0}</type>
    <folder>{1}</folder>
</listMetadataQuery>";
            string query = "";
            for(int i = 0; i < 3 && i < mdo.Length; i++)
            {
                query += string.Format(queryTemplate, mdo[i].type, mdo[i].folder);
            }

        string soapBody = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tns=""http://soap.sforce.com/2006/04/metadata"">
    <soapenv:Header>
        <tns:SessionHeader>
            <tns:sessionId>{0}</tns:sessionId>
        </tns:SessionHeader>
    </soapenv:Header>
    <soapenv:Body>
        <tns:listMetadata>
            {2}
            <asOfVersion>{1:F1}</asOfVersion>
        </tns:listMetadata>
    </soapenv:Body>
</soapenv:Envelope>", token.access_token, version, query);

            try
            {

                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, token.instance_url + "/services/Soap/m/61.0"))
                {
                    soapBody = soapBody.Replace("\n", "").Replace("\r", "");
                    using (StringContent content = new StringContent(soapBody, Encoding.UTF8, "text/xml"))
                    {
                        request.Headers.Add("Accept", "text/xml");
                        request.Headers.Add("charset", "UTF-8");
                        request.Headers.Add("SOAPAction", "login");
                        request.Headers.Add("If-Modified-Since", modifiedSince.ToString("R"));//Wish it supported this... :(
                        request.Content = content;

                        using (HttpResponseMessage response = await client.SendAsync(request))
                        {
                            response.EnsureSuccessStatusCode();
                            string retValue = await response.Content.ReadAsStringAsync();

                            return retValue;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return "";
        }


    }
}