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

builder.Services.AddDbContext<UserDb>(o => o.UseNpgsql(cfg.GetConnectionString("Default")!), ServiceLifetime.Transient);

builder.Services.AddMassTransit(x =>
{
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
        }
    );
});

var app = builder.Build();

app.MapPost(
    "/users",
    async (string name, UserDb db, IPublishEndpoint pub) =>
    {
        var u = new User { Name = name };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        await pub.Publish(new UserCreated(u.Id, u.Name));
        return Results.Created($"/users/{u.Id}", u);
    }
);

app.MapGet("/users", async (UserDb db) => await db.Users.ToListAsync());
app.Run();

public class UserDb(DbContextOptions<UserDb> o) : DbContext(o)
{
    public DbSet<User> Users => Set<User>();
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
