using System.Text;
using System.Threading.RateLimiting;
using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Auth;
using GestaoSorveteria.Application.Comandas;
using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Application.Produtos;
using GestaoSorveteria.Application.Sync;
using GestaoSorveteria.Infrastructure;
using GestaoSorveteria.Infrastructure.Seed;
using GestaoSorveteria.Server.Configuracao;
using GestaoSorveteria.Server.Middleware;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Plataformas como Railway informam a porta pela variável PORT.
var porta = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(porta))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{porta}");
}

// ---------- Infraestrutura (banco, repositórios, hash, relógio, seed) ----------
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings:Default (appsettings.Development.json) ou a variável de ambiente ConnectionStrings__Default.");
}

builder.Services.AddInfrastructure(connectionString);

// Atrás do proxy da hospedagem (Railway, Render, Fly...), o IP real do cliente vem em X-Forwarded-For.
// Sem isto, o rate limit do login (RN-US-05) trataria todos os celulares como um único IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.Secao));

// ---------- Aplicação ----------
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProdutoService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<EstornoService>();
builder.Services.AddScoped<CaixaConsultaService>();
builder.Services.AddScoped<ComandaConsultaService>();

// ---------- Versão mínima do app (GET /api/versao) ----------
var appVersao = builder.Configuration.GetSection(AppVersaoOptions.Secao).Get<AppVersaoOptions>() ?? new AppVersaoOptions();
appVersao.Validar();
builder.Services.AddSingleton(appVersao);

// ---------- Autenticação JWT (app do atendente) ----------
var jwt = builder.Configuration.GetSection(JwtOptions.Secao).Get<JwtOptions>() ?? new JwtOptions();
jwt.Validar();
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // mantém "sub", "name", "role" como estão no token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = JwtTokenService.ClaimNome,
            RoleClaimType = JwtTokenService.ClaimPerfil,
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Politicas.Admin, policy => policy.RequireRole(Politicas.PerfilAdmin));
});

// ---------- RN-US-05: limite de tentativas de login ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy<string>(RateLimitPolicies.Login, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// ---------- CORS: só o painel da dona chama a API pelo navegador (docs/07) ----------
var origensPainel = CorsPainel.LerOrigens(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPainel.Politica, policy => policy
        .WithOrigins(origensPainel)
        .AllowAnyHeader()
        .AllowAnyMethod()
        // A API gratuita demora a acordar: evita repetir a pré-verificação (OPTIONS) a cada chamada.
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
});

// ---------- API ----------
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Gestão Sorveteria — API",
        Version = "v1",
        Description = "API do app do atendente. Faça login em POST /api/auth/login e use o botão Authorize com o token.",
    });

    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Cole apenas o token (sem a palavra Bearer).",
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = [],
    });
});

var app = builder.Build();

await app.PrepararBancoAsync();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Configuration.GetValue<bool>("Swagger:Habilitado"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Gestão Sorveteria v1");
        options.DocumentTitle = "Gestão Sorveteria — API";
    });
}

// Antes do rate limit e da autenticação: a pré-verificação do navegador não conta como tentativa de login.
app.UseCors(CorsPainel.Politica);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new
{
    sistema = "Gestão Sorveteria",
    versao = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
    documentacao = "/swagger",
    saude = "/health",
}));

app.Run();

/// <summary>Permite referenciar o host em testes de integração (WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program
{
}
