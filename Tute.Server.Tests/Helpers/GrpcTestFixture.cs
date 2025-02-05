using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Tute.Server.Tests.Helpers;

public class GrpcTestFixture<TStartup> : IAsyncDisposable
    where TStartup : class
{
    private TestServer? _server;
    private WebApplication? _host;
    private HttpMessageHandler? _handler;
    private Action<IWebHostBuilder>? _configureWebHost;

    public void ConfigureWebHost(Action<IWebHostBuilder> configure)
    {
        _configureWebHost = configure;
    }

    private void EnsureServer()
    {
        if (_host == null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = Program.ConfigureServer(builder);
            app.RunAsync();
            _host = app;
            _server = _host.GetTestServer();
            _handler = _server.CreateHandler();
        }
    }

    public HttpMessageHandler Handler
    {
        get
        {
            EnsureServer();
            return _handler!;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _handler?.Dispose();
        if (_host is not null)
            await _host.DisposeAsync();
        _server?.Dispose();
    }
}
