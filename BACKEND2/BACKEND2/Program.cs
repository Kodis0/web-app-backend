using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Добавляем конфигурацию из appsettings.json
builder.Configuration.AddJsonFile("appsettings.json");

// Настраиваем Entity Framework и PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Добавляем сервис CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

builder.Services.AddControllers();

var app = builder.Build();

// Настраиваем middleware
app.UseRouting();
app.UseCors("AllowAll");
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.MapPost("/api/your-endpoint", async (HttpContext context) =>
{
    try
    {
        var formData = await context.Request.ReadFormAsync();
        var country = formData["country"].ToString();
        var phoneNumber = formData["phoneNumber"].ToString();
        var password = formData["password"].ToString();

        // Проверка, существует ли номер телефона
        using (var saveScope = app.Services.CreateScope())
        {
            var dbContext = saveScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var phoneNumberExists = await dbContext.Users.AnyAsync(u => u.PhoneNumber == phoneNumber);

            if (phoneNumberExists)
            {
                context.Response.StatusCode = 409; // Conflict
                var conflictResponse = new { message = "Phone number already exists" };
                await context.Response.WriteAsJsonAsync(conflictResponse);
                return;
            }

            // Создаем новую сущность User
            var user = new User
            {
                Country = country,
                PhoneNumber = phoneNumber,
                Password = password 
            };

            // Сохраняем пользователя в базе данных
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        // Возвращаем успешный ответ
        context.Response.StatusCode = 200;
        var successResponse = new { message = "Data saved successfully." };
        await context.Response.WriteAsJsonAsync(successResponse);
    }
    catch (Exception ex)
    {
        // Обрабатываем ошибки и возвращаем ответ с ошибкой
        context.Response.StatusCode = 500;
        var errorResponse = new { message = $"Произошла ошибка: {ex.Message}" };
        await context.Response.WriteAsJsonAsync(errorResponse);
    }
});

app.MapPost("/api/check-user", async (HttpContext context) =>
{
    try
    {
        var formData = await context.Request.ReadFormAsync();
        var phoneNumber = formData["phoneNumber"].ToString();
        var password = formData["password"].ToString();

        using (var saveScope = app.Services.CreateScope())
        {
            var dbContext = saveScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && u.Password == password);

            if (user == null)
            {
                context.Response.StatusCode = 401; // Unauthorized
                var unauthorizedResponse = new { message = "Incorrect phone number or password" };
                await context.Response.WriteAsJsonAsync(unauthorizedResponse);
                return;
            }
        }

        context.Response.StatusCode = 200;
        var successResponse = new { message = "User authenticated successfully" };
        await context.Response.WriteAsJsonAsync(successResponse);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        var errorResponse = new { message = $"An error occurred: {ex.Message}" };
        await context.Response.WriteAsJsonAsync(errorResponse);
    }
});


// Проверяем наличие миграций и применяем их при необходимости
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (dbContext.Database.GetPendingMigrations().Any())
    {
        dbContext.Database.Migrate();
    }
}

app.Run();

// Класс сущности User
public class User
{
    public int Id { get; set; }
    public string Country { get; set; }
    public string PhoneNumber { get; set; }
    public string Password { get; set; }
}

// Класс DbContext для EF Core
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>()
            .HasIndex(u => u.PhoneNumber)
            .IsUnique(); // Уникальный индекс на номер телефона
    }
}
