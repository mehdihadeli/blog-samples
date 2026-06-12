using Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var vaultSettingsPath = builder.Environment.IsDevelopment()
    ? Environment.GetEnvironmentVariable("VAULT_SECRETS_PATH")
        ?? $"{builder.Environment.ContentRootPath}/vault-secrets/appsettings.json"
    : "/vault/secrets/appsettings.json";
builder.Configuration.AddJsonFile(vaultSettingsPath, optional: true, reloadOnChange: true);

var cfg = builder.Configuration;

// Database – transient to pick up rotated creds
builder.Services.AddDbContext<OrderDb>(
    o => o.UseNpgsql(cfg.GetConnectionString("Default")!),
    ServiceLifetime.Transient
);

// MassTransit RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserCreatedConsumer>();
    x.UsingRabbitMq(
        (ctx, r) =>
        {
            var rmq = cfg.GetSection("RabbitMQ");
            r.Host(
                builder.Environment.IsDevelopment() ? "localhost" : rmq["Host"],
                ushort.Parse(rmq["Port"]!),
                "/",
                h =>
                {
                    h.Username(rmq["Username"]!);
                    h.Password(rmq["Password"]!);
                }
            );
            r.ReceiveEndpoint("orders-user-created", e => e.ConfigureConsumer<UserCreatedConsumer>(ctx));
        }
    );
});

var app = builder.Build();
app.MapGet("/orders", async (OrderDb db) => await db.Orders.ToListAsync());
app.Run();

// Domain
public class OrderDb(DbContextOptions<OrderDb> o) : DbContext(o)
{
    public DbSet<Order> Orders => Set<Order>();
}

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
}

public class UserCreatedConsumer(OrderDb db) : IConsumer<UserCreated>
{
    public async Task Consume(ConsumeContext<UserCreated> context)
    {
        db.Orders.Add(new Order { UserId = context.Message.Id, UserName = context.Message.Name });
        await db.SaveChangesAsync();
    }
}
