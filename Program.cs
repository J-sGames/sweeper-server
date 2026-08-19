using SweeperServer.Data;
using SweeperServer.Services;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SweeperDB");

builder.Services.AddDbContext<SweeperDbContext>(options=> 
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)));

builder.Services.AddControllers();
builder.Services.AddScoped<PlayLogService>();

var app = builder.Build();

app.MapControllers();

app.Run();