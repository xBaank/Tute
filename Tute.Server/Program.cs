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
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                // WARN: Do not apply following policies to your production.
                //       If not configured carefully, it may cause security problems.
                policy.AllowAnyMethod();
                policy.AllowAnyOrigin();
                policy.AllowAnyHeader();

                // NOTE: "grpc-status" and "grpc-message" headers are required by gRPC. so, we need expose these headers to the client.
                policy.WithExposedHeaders("grpc-status", "grpc-message");
            });
        });
        builder.Services.AddMagicOnion();
        builder.Services.AddSingleton<ConcurrentDictionary<string, GameRoom>>();
        builder
            .WebHost.UseUrls("http://*:5000")
            .ConfigureKestrel(
                (options) =>
                {
                    options.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http1AndHttp2);
                }
            );

        var app = builder.Build();

        app.UseCors();
        app.UseWebSockets();
        app.UseGrpcWebSocketRequestRoutingEnabler();

        app.UseRouting();

        // NOTE: `UseGrpcWebSocketBridge` must be called after calling `UseRouting`.
        app.UseGrpcWebSocketBridge();

        app.MapMagicOnionService();
        return app;
    }
}
