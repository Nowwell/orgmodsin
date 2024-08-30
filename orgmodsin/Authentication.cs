using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace orgmodsin
{

    public class Authentication
    {
        private readonly static bool IsLinux = (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX);
        private readonly static string redirectUri = "http://localhost:1717/";
        private readonly static string DIRNAME = ".orgmod";
        private readonly static string PROD_ENDPOINT = "https://login.salesforce.com";
        private readonly static string TEST_ENDPOINT = "https://test.salesforce.com";
        private readonly static string? homePath = IsLinux ? Environment.GetEnvironmentVariable("HOME") : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
        private static DirectoryInfo credPath = new DirectoryInfo(Path.Combine(homePath == null? "" : homePath, DIRNAME));

        private static string HTML = @"<!DOCTYPE html><html><head>
<style>body{font-family:sans-serif;}dt{font-weight:bold;}dd{margin-bottom:10px;}div{margin-left:auto;margin-right:auto;width:500px;background-color:whitesmoke;padding:25px;border:1px solid black;border-radius:10px;}</style>
<title>Authentication successful</title></head>
<body><br><br><br><br><div><h1>Authentication successful</h1><p>You can now close this page.</p></div></body>
</html>";

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

        public async static Task<TokenResponse?> Authenticate(string currentUser, TokenResponse token, bool istest)
        {
            return await Authenticate(currentUser, token.clientid, token.clientsecret, istest);
        }

        public async static Task<TokenResponse?> Authenticate(string currentUser, string clientid, string clientsecret, bool istest)
        {
            clientid = HttpUtility.UrlEncode(clientid);

            //https://github.com/googlesamples/oauth-apps-for-windows/blob/master/OAuthConsoleApp/OAuthConsoleApp/Program.cs

            string state = GenerateRandomDataBase64url(32);
            string codeVerifier = GenerateRandomDataBase64url(32);
            string codeChallenge = Base64UrlEncodeNoPadding(Sha256Ascii(codeVerifier));

            // Creates an HttpListener to listen for requests on that redirect URI.
            var http = new HttpListener();
            http.Prefixes.Add(redirectUri);
            http.Start();

            string authorizationRequest = string.Format("{0}/services/oauth2/authorize?response_type=code&scope=api+web&redirect_uri={1}&client_id={2}&state={3}&code_challenge={4}",
                PROD_ENDPOINT,
                Uri.EscapeDataString("http://localhost:1717/"),
                clientid,
                state,
                codeChallenge);

            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
            info.FileName = authorizationRequest;
            info.UseShellExecute = true;
            System.Diagnostics.Process.Start(info);

            HttpListenerContext context = await http.GetContextAsync();

            HttpListenerResponse response = context.Response;
            string responseString = HTML;
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            Stream responseOutput = response.OutputStream;
            await responseOutput.WriteAsync(buffer, 0, buffer.Length);
            responseOutput.Close();
            http.Stop();

            string? code = context.Request.QueryString.Get("code");
            string? incomingState = context.Request.QueryString.Get("state");

            if(code == null || incomingState == null)
            {
                Console.WriteLine("Invalid OAuth response");
                return null;
            }

            if (incomingState != state)
            {
                Console.WriteLine("Invalid OAuth State");
                return null;
            }

            using HttpClient client = new HttpClient();
            TokenResponse? token = await GetToken(client, clientid, clientsecret, (code == null ? "" : code), codeVerifier, istest);

            if (token != null)
            {
                if (!credPath.Exists)
                {
                    credPath.Create();
                    credPath.Attributes = FileAttributes.Archive | FileAttributes.Hidden;
                }
                using (StreamWriter output = new StreamWriter(Path.Combine(credPath.FullName, currentUser)))
                {
                    token.clientid = clientid;
                    token.clientsecret = clientsecret;
                    output.WriteLine(JsonSerializer.Serialize(token));
                    output.Flush();
                }

                FileInfo fi = new FileInfo(Path.Combine(credPath.FullName, "current"));
                if (!fi.Exists)
                {
                    SetDefaultUser(currentUser);
                }
            }
            else
            {
                Console.WriteLine("Unable to get OAuth token");
            }

            return token;
        }

        //public async static Task<DeviceCodeResponse?> GetDeviceCode(HttpClient client, string clientid, bool istest)
        //{
        //    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, (istest ? TEST_ENDPOINT : PROD_ENDPOINT) + "/services/oauth2/token");
        //    using StringContent content = new StringContent(string.Format("response_type=device_code&client_id={0}&scope=api web", clientid), Encoding.UTF8, "application/x-www-form-urlencoded");
        //    request.Content = content;

        //    using HttpResponseMessage response = await client.SendAsync(request);


        //    return JsonSerializer.Deserialize<DeviceCodeResponse>(await response.Content.ReadAsStringAsync());
        //}

        public async static Task<TokenResponse?> GetToken(HttpClient client, string clientid, string clientsecret, string code, string verifier, bool istest)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, (istest ? TEST_ENDPOINT : PROD_ENDPOINT) + "/services/oauth2/token");
            using StringContent content = new StringContent(string.Format("grant_type=authorization_code&client_id={0}&client_secret={3}&code={1}&&code_verifier={2}&redirect_uri={4}", clientid, code, verifier, clientsecret, Uri.EscapeDataString(redirectUri)), Encoding.UTF8, "application/x-www-form-urlencoded");
            request.Content = content;

            using HttpResponseMessage response = await client.SendAsync(request);

            TokenResponse? token = null;
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                token = JsonSerializer.Deserialize<TokenResponse>(await response.Content.ReadAsStringAsync());
            }
            else
            {
                Console.WriteLine(await response.Content.ReadAsStringAsync());
            }

            return token;
        }

        public async static Task<TokenResponse?> GetToken(HttpClient client, string clientid, DeviceCodeResponse deviceCode, bool istest)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, (istest ? TEST_ENDPOINT : PROD_ENDPOINT) + "/services/oauth2/token");
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

        public async static Task<DeviceCodeResponse?> GetTokenFromRefresh(HttpClient client, string clientid, string clientSecret, string refreshToken, bool istest)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, (istest ? TEST_ENDPOINT : PROD_ENDPOINT) + "/services/oauth2/token");
            using StringContent content = new StringContent(string.Format("response_type=refresh_token&client_id={0}&client_secret={1}&refresh_token={2}", clientid, clientSecret, refreshToken), Encoding.UTF8, "application/x-www-form-urlencoded");
            request.Content = content;

            using HttpResponseMessage response = await client.SendAsync(request);


            return JsonSerializer.Deserialize<DeviceCodeResponse>(await response.Content.ReadAsStringAsync());
        }

        public static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string GenerateRandomDataBase64url(uint length)
        {
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            byte[] bytes = new byte[length];
            rng.GetBytes(bytes);
            return Base64UrlEncodeNoPadding(bytes);
        }

        private static byte[] Sha256Ascii(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(bytes);
            }
        }

        private static string Base64UrlEncodeNoPadding(byte[] buffer)
        {
            string base64 = Convert.ToBase64String(buffer);

            // Converts base64 to base64url.
            base64 = base64.Replace("+", "-");
            base64 = base64.Replace("/", "_");
            // Strips padding.
            base64 = base64.Replace("=", "");

            return base64;
        }
    }
}