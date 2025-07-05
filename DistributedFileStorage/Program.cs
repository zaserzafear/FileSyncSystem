
using DistributedFileStorage.Caching;
using DistributedFileStorage.Data;
using DistributedFileStorage.Messaging;
using DistributedFileStorage.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Reflection;

namespace DistributedFileStorage;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddHealthChecks();

        // Add DbContext
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            var mysqlConnStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
            options.UseMySql(mysqlConnStr, MySqlServerVersion.AutoDetect(mysqlConnStr));
        });

        // Add RabbitMQ Message Bus
        builder.Services.AddRabbitMqMessageBus(
            builder.Configuration.GetValue<string>("RabbitMq:AmqpUri")!);
        builder.Services.AddMessageConsumers(Assembly.GetExecutingAssembly());

        // Add Redis connection multiplexer as singleton
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisConnStr = builder.Configuration.GetValue<string>("Redis:ConnectionString")!;
            return ConnectionMultiplexer.Connect(redisConnStr);
        });

        // Add RedisFileCache implementation for IFileCache
        builder.Services.AddScoped<IFileCache, RedisFileCache>();

        // Register FileStorageService
        builder.Services.AddScoped<IFileService, FileService>();

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapHealthChecks("health");

        app.UseAuthorization();


        app.MapControllers();

        app.Run();
    }
}
