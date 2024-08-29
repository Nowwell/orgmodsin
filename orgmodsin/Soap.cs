using System.Text;
using System.Xml.Serialization;

public class Soap
{
    public async static Task<String> DescribeMetadata(HttpClient client, TokenResponse token)
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
<asOfVersion>61.0</asOfVersion>
</tns:describeMetadata>
</soapenv:Body>
</soapenv:Envelope>", token.access_token);

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