using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Tute.Server.Services;

var builder = WebApplication.CreateBuilder(args);
ConfigureServer(builder).Run();

public partial class Program
{
    public static WebApplication ConfigureServer(WebApplicationBuilder builder)
    {
        builder.Services.AddGrpc();
        builder.Services.AddMagicOnion();
        builder.Services.AddSingleton<ConcurrentDictionary<string, GameRoom>>();
        builder.WebHost.ConfigureKestrel(
            (options) =>
            {
                options.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http2);
            }
        );

        var app = builder.Build();

        app.MapMagicOnionService();
        return app;
    }
}