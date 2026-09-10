using MassTransit;
using PayFlow.Notification.Worker.Consumers;

var builder = Host.CreateApplicationBuilder(args);

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
