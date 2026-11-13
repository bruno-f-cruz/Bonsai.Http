using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using Bonsai.Http;
using Bonsai.Http.Server;

using var portLease = new TcpListener(IPAddress.Loopback, 0);
portLease.Start();
var port = ((IPEndPoint)portLease.LocalEndpoint).Port;
portLease.Stop();
var address = $"http://127.0.0.1:{port}";
using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(2));
using var subscription = server.RegisterRoute("GET", "/animals/{id}").Subscribe(request =>
    request.Reply(new HttpServerResponse(200, Encoding.UTF8.GetBytes("{\"name\":\"otter\"}"), "application/json")));
await server.StartAsync();
using var client = new HttpClientResource(new Uri(address), new Dictionary<string, string>(),
    TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(5), 1024);
var request = new HttpRequest("GET", "/animals/1");
var response = await client.SendAsync(request);
if (response.StatusCode != 200 || response.Request != request || Encoding.UTF8.GetString(response.Body) != "{\"name\":\"otter\"}")
    throw new Exception("GET response mismatch.");
var notFound = await client.SendAsync(new HttpRequest("GET", "/missing"));
if (notFound.StatusCode != 404) throw new Exception("Expected 404 as response data.");
try
{
    await new EnsureSuccessStatusCode().Process(Observable.Return(notFound)).FirstAsync();
    throw new Exception("Expected explicit status error.");
}
catch (HttpStatusException) { }
Console.WriteLine("HTTP client/server smoke passed.");
