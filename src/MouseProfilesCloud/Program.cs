using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MouseProfilesCloud;
using MouseProfilesCloud.Data;
using MouseProfilesCloud.Options;
using MouseProfilesCloud.Repositories;
using MouseProfilesCloud.Services;

var builder = WebApplication.CreateBuilder(args);

// Options — validated eagerly at startup so a missing key fails fast
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BrevoOptions>()
    .BindConfiguration(BrevoOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AppOptions>()
    .BindConfiguration(AppOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Infrastructure
builder.Services.AddSingleton<Db>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();

// Services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IEmailService, BrevoEmailService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProfileService>();

// ICurrentUser reads from the HTTP context
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// JWT auth — MapInboundClaims = false keeps "sub" as "sub" (not mapped to NameIdentifier)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()!;
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();

// ProblemDetails for uniform error responses
builder.Services.AddProblemDetails();

// Global exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Swagger with JWT bearer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MouseProfiles Cloud API",
        Version = "v1",
        Description = "Cloud backup and sync for the MouseProfiles desktop app.",
    });

    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT returned by POST /auth/login.",
    });

    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
