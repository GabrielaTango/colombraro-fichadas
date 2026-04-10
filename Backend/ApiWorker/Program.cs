using FichadasAPI.Data;
using FichadasAPI.Repositories;
using FichadasAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.IdentityModel.Tokens;
using System.Text;

try
{
    var options = new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = OperatingSystem.IsWindows() && IsRunningAsWindowsService()
            ? AppContext.BaseDirectory
            : default
    };

    var builder = WebApplication.CreateBuilder(options);

    // Configurar como Windows Service solo en Windows
    if (OperatingSystem.IsWindows())
    {
        builder.Host.UseWindowsService();
    }

    // Configurar logging (sin EventLog para evitar problemas de compatibilidad)
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();

    // Add services to the container.
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Configurar Dapper Context
    builder.Services.AddSingleton<DapperContext>();

    // Configurar Repositorios
    builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
    builder.Services.AddScoped<ISectorRepository, SectorRepository>();
    builder.Services.AddScoped<IEmpleadoRepository, EmpleadoRepository>();
    builder.Services.AddScoped<IFichadaRepository, FichadaRepository>();
    builder.Services.AddScoped<IConfiguracionCalculoRepository, ConfiguracionCalculoRepository>();
    builder.Services.AddScoped<INovedadRepository, NovedadRepository>();

    // Configurar Servicios
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IFichadaImportService, FichadaImportService>();
    builder.Services.AddScoped<IHorasCalculoService, HorasCalculoService>();
    builder.Services.AddScoped<IFichadaExportService, FichadaExportService>();

    // Configurar JWT Authentication
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT Secret Key not configured");

    builder.Services.AddAuthentication(opt =>
    {
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

    // Configurar CORS
    builder.Services.AddCors(opt =>
    {
        opt.AddPolicy("AllowReactApp",
            policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
    });

    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Fichadas API",
            Version = "v1"
        });

        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Ingrese el token JWT con el formato: Bearer {token}"
        });

        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    app.UseCors("AllowReactApp");
    // Log startup
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("FichadasAPI iniciando... ContentRoot: {ContentRoot}", builder.Environment.ContentRootPath);

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }


    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    logger.LogInformation("FichadasAPI configurado correctamente, iniciando servidor...");
    app.Run();
}
catch (Exception ex)
{
    // Log fatal errors to Event Log (solo Windows)
    if (OperatingSystem.IsWindows())
    {
        try
        {
            using var eventLog = new System.Diagnostics.EventLog("Application");
            eventLog.Source = "FichadasAPI";
            eventLog.WriteEntry($"Error fatal al iniciar FichadasAPI: {ex}", System.Diagnostics.EventLogEntryType.Error);
        }
        catch
        {
            // Ignorar errores de EventLog
        }
    }
    Console.Error.WriteLine($"Error fatal al iniciar FichadasAPI: {ex}");
    throw;
}

// Helper para detectar si está corriendo como Windows Service
static bool IsRunningAsWindowsService()
{
    if (!OperatingSystem.IsWindows())
        return false;

    // En Windows, verificar si el proceso padre es services.exe
    try
    {
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        // Si no hay consola, probablemente es un servicio
        return Environment.UserInteractive == false;
    }
    catch
    {
        return false;
    }
}
