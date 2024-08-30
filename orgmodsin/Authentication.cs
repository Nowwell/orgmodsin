using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using System.Security.Cryptography;
using System.Diagnostics;

namespace orgmodsin
{

    public class Authentication
    {
        static readonly bool IsLinux = (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX);

        private readonly static string DIRNAME = ".sfc";
        private readonly static string PROD_ENDPOINT = "https://login.salesforce.com";//https://publicissapient296-dev-ed.my.salesforce.com
        //private readonly static string TEST_ENDPOINT = "https://test.salesforce.com";
        private readonly static string CLIENTID = "3MVG9szVa2RxsqBaFfy.QEDHf2vDia__JDsaFvn0p5jldHqxf5.I_5IiwAEq1Yw3M00GPEKjbcHbQKjwwPmga";//"3MVG9gTv.DiE8cKRZOOKNIsODKw27hZrbQ7KHJfDKTVlfiqOM0XwgTztju0BqLn71IsJcke.iTqVKeOg4TtCk";
        //private readonly static string CLIENTSECRET = "F3112592DF897172563007F352F1FE4D414FBE0E63EEA92609899F6000AEDA57";

        private static string? homePath = IsLinux ? Environment.GetEnvironmentVariable("HOME") : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

        private static DirectoryInfo credPath = new DirectoryInfo(Path.Combine(homePath == null? "" : homePath, DIRNAME));

        public static bool DoesUserExist(string user)
        {
            return new FileInfo(Path.Combine(credPath.FullName, user)).Exists;
        }

        public static string DefaultUser()
        {
            FileInfo current = new FileInfo(Path.Combine(credPath.FullName, "current"));
            string currentUser = "";
            if (current.Exists)
            {
                using (StreamReader currentUserFile = new StreamReader(Path.Combine(credPath.FullName, "current")))
                {
                    currentUser = currentUserFile.ReadToEnd();
                }
            }

            return currentUser;
        }

        public static void SetDefaultUser(string user)
        {
            if (credPath.GetFiles(user).Length == 1)
            {
                using (StreamWriter output = new StreamWriter(Path.Combine(credPath.FullName, "current")))
                {
                    output.WriteLine(user);
                    output.Flush();
                }
            }
        }

        public static TokenResponse? LoadUser(string currentUser)
        {
            if (!credPath.Exists) credPath.Create();

            TokenResponse? token = null;
            using (StreamReader currentUserTokenFile = new StreamReader(Path.Combine(credPath.FullName, currentUser)))
            {
                token = JsonSerializer.Deserialize<TokenResponse>(currentUserTokenFile.ReadToEnd());
            }

            return token;
        }

        public async static Task<TokenResponse?> Authenticate(string currentUser)
        {
            string clientid = HttpUtility.UrlEncode(CLIENTID);


            //string state = GenerateRandomDataBase64url(32);
            //string codeVerifier = GenerateRandomDataBase64url(32);
            //string codeChallenge = Base64UrlEncodeNoPadding(Sha256Ascii(codeVerifier));
            //const string codeChallengeMethod = "S256";


            //// Creates a redirect URI using an available port on the loopback address.
            //string redirectUri = $"http://localhost:8080/"; //{IPAddress.Loopback}:{GetRandomUnusedPort()}

            //// Creates an HttpListener to listen for requests on that redirect URI.
            //var http = new HttpListener();
            //http.Prefixes.Add(redirectUri);
            //http.Start();

            //string authorizationRequest = string.Format("{0}?response_type=code&scope=api+web%20profile&redirect_uri={1}&client_id={2}&state={3}&code_challenge={4}&code_challenge_method={5}",
            //    PROD_ENDPOINT,
            //    Uri.EscapeDataString(redirectUri),
            //    clientid,
            //    state,
            //    codeChallenge,
            //    codeChallengeMethod);


            //System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
            //info.FileName = authorizationRequest;
            //info.UseShellExecute = true;

            //Process p = System.Diagnostics.Process.Start(info);

            //var context = await http.GetContextAsync();

            //var response = context.Response;
            //string responseString = "<html><head></head><body>Please return to the app.</body></html>";
            //byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            //response.ContentLength64 = buffer.Length;
            //var responseOutput = response.OutputStream;
            //await responseOutput.WriteAsync(buffer, 0, buffer.Length);
            //responseOutput.Close();
            //http.Stop();

            //var code = context.Request.QueryString.Get("code");
            //var incomingState = context.Request.QueryString.Get("state");






            using HttpClient client = new HttpClient();

            DeviceCodeResponse? deviceCode = await GetDeviceCode(client, clientid);

            if (deviceCode == null)
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
            while (DateTime.Now < timeToStop)
            {
                if (token == null)
                {
                    System.Threading.Thread.Sleep(5000);
                }
                else
                {
                    break;
                }

                token = await GetToken(client, clientid, deviceCode);
            }

            if (token != null)
            {
                if (!credPath.Exists) credPath.Create();
                using (StreamWriter output = new StreamWriter(Path.Combine(credPath.FullName, currentUser)))
                {
                    output.WriteLine(JsonSerializer.Serialize(token));
                    output.Flush();
                }

                FileInfo fi = new FileInfo(Path.Combine(credPath.FullName, "current"));
                if (!fi.Exists)
                {
                    SetDefaultUser(currentUser);
                }
            }

            return token;
        }

        public async static Task<DeviceCodeResponse?> GetDeviceCode(HttpClient client, string clientid)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, PROD_ENDPOINT + "/services/oauth2/token");
            using StringContent content = new StringContent(string.Format("response_type=device_code&client_id={0}&scope=api web", clientid), Encoding.UTF8, "application/x-www-form-urlencoded");
            request.Content = content;

            using HttpResponseMessage response = await client.SendAsync(request);


            return JsonSerializer.Deserialize<DeviceCodeResponse>(await response.Content.ReadAsStringAsync());
        }

        public async static Task<TokenResponse?> GetToken(HttpClient client, string clientid, DeviceCodeResponse deviceCode)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, PROD_ENDPOINT + "/services/oauth2/token");
            using StringContent content = new StringContent(string.Format("grant_type=device&client_id={0}&code={1}", clientid, deviceCode.device_code), Encoding.UTF8, "application/x-www-form-urlencoded");
            request.Content = content;

            using HttpResponseMessage response = await client.SendAsync(request);

            TokenResponse? token = null;
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                token = JsonSerializer.Deserialize<TokenResponse>(await response.Content.ReadAsStringAsync());
            }

            return token;
        }

        public async static Task<DeviceCodeResponse?> GetTokenFromRefresh(HttpClient client, string clientid, string clientSecret, string refreshToken)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, PROD_ENDPOINT + "/services/oauth2/token");
            using StringContent content = new StringContent(string.Format("response_type=refresh_token&client_id={0}&client_secret={1}&refresh_token={2}", clientid, clientSecret, refreshToken), Encoding.UTF8, "application/x-www-form-urlencoded");
            request.Content = content;

            using HttpResponseMessage response = await client.SendAsync(request);


            return JsonSerializer.Deserialize<DeviceCodeResponse>(await response.Content.ReadAsStringAsync());
        }


        //public static int GetRandomUnusedPort()
        //{
        //    var listener = new TcpListener(IPAddress.Loopback, 0);
        //    listener.Start();
        //    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        //    listener.Stop();
        //    return port;
        //}

        //private static string GenerateRandomDataBase64url(uint length)
        //{
        //    RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        //    byte[] bytes = new byte[length];
        //    rng.GetBytes(bytes);
        //    return Base64UrlEncodeNoPadding(bytes);
        //}

        //private static byte[] Sha256Ascii(string text)
        //{
        //    byte[] bytes = Encoding.ASCII.GetBytes(text);
        //    using (SHA256Managed sha256 = new SHA256Managed())
        //    {
        //        return sha256.ComputeHash(bytes);
        //    }
        //}
        //private static string Base64UrlEncodeNoPadding(byte[] buffer)
        //{
        //    string base64 = Convert.ToBase64String(buffer);

        //    // Converts base64 to base64url.
        //    base64 = base64.Replace("+", "-");
        //    base64 = base64.Replace("/", "_");
        //    // Strips padding.
        //    base64 = base64.Replace("=", "");

        //    return base64;
        //}
    }
}