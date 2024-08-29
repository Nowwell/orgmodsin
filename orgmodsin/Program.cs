string chosenUser = Authentication.DefaultUser();
bool needsToAuth = false;
string includes = "";
string excludes = "";
string api = "61";

for (int i = 0; i < args.Length; i++)
{
    if ((args[i] == "--user" || args[i] == "-u") && i + 1 < args.Length)
    {
        chosenUser = args[++i];
    }
    if ((args[i] == "--alias" || args[i] == "-a") && i + 1 < args.Length)
    {
        needsToAuth = true;
        chosenUser = args[++i];
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
        api = args[++i];
    }
}

TokenResponse? token = null;
if (needsToAuth == true)
{
    token = await Authentication.Authenticate(chosenUser);
    if (token == null)
    {
        Console.WriteLine("Authentication failure.");
        return;
    }
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

//If-Modified-Since: Tue, 23 Mar 2015 00:00:00 GMT

//Metadata types to include, exclude
//string bearer = "Bearer " + token.access_token;
//string endpoint = token.instance_url + $"/services/data/v{api}.0/metadata";

using HttpClient client = new HttpClient();

// using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint);
// request.Headers.Add("Authorization",bearer);

// using HttpResponseMessage response = await client.SendAsync(request);

Console.WriteLine(Soap.DescribeMetadata(client, token));

// Console.WriteLine(await response.Content.ReadAsStringAsync());