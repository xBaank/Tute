using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Tute.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc(); // Add this line(Grpc.AspNetCore)
builder.Services.AddMagicOnion(); // Add this line(MagicOnion.Server)
builder.Services.AddSingleton<Dictionary<string, GameRoom>>();
builder.WebHost.ConfigureKestrel(
    (options) =>
    {
        options.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http2);
    }
);

var app = builder.Build();

app.MapMagicOnionService(); // Add this line

app.Run();
