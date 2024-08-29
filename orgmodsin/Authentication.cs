using System.Text;
using System.Text.Json;
using System.Web;

public class Authentication
{
    private static string homePath = (Environment.OSVersion.Platform == PlatformID.Unix || 
                   Environment.OSVersion.Platform == PlatformID.MacOSX)
            ? Environment.GetEnvironmentVariable("HOME")
            : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

    private static DirectoryInfo credPath = new DirectoryInfo(Path.Combine(homePath, ".sfc"));
    
    public static string DefaultUser()
    {
        FileInfo current = new FileInfo(Path.Combine(credPath.FullName, "current"));
        string currentUser = "";
        if(current.Exists)
        {
            using(StreamReader currentUserFile = new StreamReader(Path.Combine(credPath.FullName, "current")))
            {
                currentUser = currentUserFile.ReadToEnd();
            }
        }

        return currentUser;
    }

    public static void SetDefaultUser(string user)
    {
        if(credPath.GetFiles(user).Length == 1)
        {
            using(StreamWriter output = new StreamWriter(Path.Combine(credPath.FullName, "current")))
            {
                output.WriteLine(user);
                output.Flush();
            }
        }
    }

    public static TokenResponse? LoadUser(string currentUser)
    {        
        if(!credPath.Exists) credPath.Create();

        TokenResponse? token = null;
        using(StreamReader currentUserTokenFile = new StreamReader(Path.Combine(credPath.FullName, currentUser)))
        {
            token = JsonSerializer.Deserialize<TokenResponse>(currentUserTokenFile.ReadToEnd());
        }

        return token;  
    }

    public async static Task<TokenResponse?> Authenticate(string currentUser)
    {
        string clientid = HttpUtility.UrlEncode("3MVG9gTv.DiE8cKRZOOKNIsODKw27hZrbQ7KHJfDKTVlfiqOM0XwgTztju0BqLn71IsJcke.iTqVKeOg4TtCk");

        using HttpClient client = new HttpClient();

        DeviceCodeResponse? deviceCode = await GetDeviceCode(client, clientid);

        if(deviceCode == null)
        {
            Console.WriteLine("Unable to start authentication process");
            return null;
        }

        System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
        info.FileName = deviceCode.verification_uri;
        info.UseShellExecute = true;

        System.Diagnostics.Process.Start(info);
        Console.WriteLine(deviceCode.user_code);

        DateTime timeToStop = DateTime.Now.AddMinutes(deviceCode.interval);

        TokenResponse? token = null;
        while(DateTime.Now < timeToStop)
        {
            if(token == null)
            {
                System.Threading.Thread.Sleep(5000);
            }
            else
            {
                break;
            }

            token = await GetToken(client, clientid, deviceCode);
        }

        if(token != null)
        {
            if(!credPath.Exists) credPath.Create();
            using(StreamWriter output = new StreamWriter(Path.Combine(credPath.FullName, currentUser)))
            {
                output.WriteLine(JsonSerializer.Serialize(token));
                output.Flush();
            }

            FileInfo fi = new FileInfo(Path.Combine(credPath.FullName, "current"));
            if(!fi.Exists)
            {
                SetDefaultUser(currentUser);
            }
        }

        return token;
    }

    public async static Task<DeviceCodeResponse?> GetDeviceCode(HttpClient client, string clientid)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://publicissapient296-dev-ed.my.salesforce.com/services/oauth2/token");
        using StringContent content = new StringContent(string.Format("response_type=device_code&client_id={0}&scope=full", clientid), Encoding.UTF8, "application/x-www-form-urlencoded");
        request.Content = content;

        using HttpResponseMessage response = await client.SendAsync(request);


        return JsonSerializer.Deserialize<DeviceCodeResponse>(await response.Content.ReadAsStringAsync());
    }

    public async static Task<TokenResponse?> GetToken(HttpClient client, string clientid, DeviceCodeResponse deviceCode)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://publicissapient296-dev-ed.my.salesforce.com/services/oauth2/token");
        using StringContent content = new StringContent(string.Format("grant_type=device&client_id={0}&code={1}", clientid, deviceCode.device_code), Encoding.UTF8, "application/x-www-form-urlencoded");
        request.Content = content;

        using HttpResponseMessage response = await client.SendAsync(request);

        TokenResponse? token = null;
        if(response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            token = JsonSerializer.Deserialize<TokenResponse>(await response.Content.ReadAsStringAsync());
        }

        return token;      
    }    

    public async static Task<DeviceCodeResponse?> GetTokenFromRefresh(HttpClient client, string clientid, string clientSecret, string refreshToken)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://publicissapient296-dev-ed.my.salesforce.com/services/oauth2/token");
        using StringContent content = new StringContent(string.Format("response_type=refresh_token&client_id={0}&client_secret={1}&refresh_token={2}", clientid, clientSecret, refreshToken), Encoding.UTF8, "application/x-www-form-urlencoded");
        request.Content = content;

        using HttpResponseMessage response = await client.SendAsync(request);


        return JsonSerializer.Deserialize<DeviceCodeResponse>(await response.Content.ReadAsStringAsync());
    }

}