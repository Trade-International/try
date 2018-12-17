using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pocket;
using Recipes;

namespace MLS.Agent.Tests
{
    public class AgentService : IDisposable
    {
        private readonly CommandLineOptions _options;
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        private readonly HttpClient _client;

        public AgentService(CommandLineOptions options = null)
        {
            _options = options ?? new CommandLineOptions(
                           production: false,
                           languageService: false);

            var testServer = CreateTestServer();

            _client = testServer.CreateClient();

            _disposables.Add(testServer);
            _disposables.Add(_client);
        }

        public void Dispose() => _disposables.Dispose();

        private TestServer CreateTestServer() => new TestServer(CreateWebHostBuilder());

        private IWebHostBuilder CreateWebHostBuilder()
        {
            var builder = new WebHostBuilder()
                          .ConfigureServices(c => { c.AddSingleton(_options); })
                          .UseTestEnvironment()
                          .UseStartup<Startup>();

            return builder;
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) =>
            _client.SendAsync(request);
    }
}