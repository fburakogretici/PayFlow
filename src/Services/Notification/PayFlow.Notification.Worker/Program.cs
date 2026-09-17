using MassTransit;
using PayFlow.Notification.Worker.Consumers;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Serilog & Seq Yapılandırması (Observability)
var seqUrl = builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341";
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "PayFlow.Notification.Worker")
    .WriteTo.Console()
    .WriteTo.Seq(seqUrl)
    .CreateLogger();

builder.Services.AddSerilog();

// MassTransit & RabbitMQ Konfigürasyonu
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentCompletedNotificationConsumer>();
    x.AddConsumer<PaymentFailedNotificationConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var user = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var pass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(host, "/", h =>
        {
            h.Username(user);
            h.Password(pass);
        });

        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
