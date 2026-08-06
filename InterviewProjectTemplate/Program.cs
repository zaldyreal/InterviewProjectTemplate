using System.Text;
using InterviewProjectTemplate.Application.Abstractions;
using InterviewProjectTemplate.Application.Options;
using InterviewProjectTemplate.Application.Security;
using InterviewProjectTemplate.Application.Services;
using InterviewProjectTemplate.Infrastructure.Identity;
using InterviewProjectTemplate.Infrastructure.Persistence;
using InterviewProjectTemplate.Infrastructure.Security;
using InterviewProjectTemplate.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace InterviewProjectTemplate
{
    public class Program
    {
        /// <summary>
        /// Named CORS policy for the Angular client. The template's original policy allowed any
        /// origin, which cannot be combined with credentialed requests — browsers reject
        /// `Access-Control-Allow-Origin: *` when the request carries cookies, and the anonymous
        /// identity cookie is exactly that. Origins are therefore explicit and configurable.
        /// </summary>
        private const string AngularCorsPolicy = "AngularClient";

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            ConfigureServices(builder);

            var app = builder.Build();

            await ConfigurePipelineAsync(app);

            await app.RunAsync();
        }

        private static void ConfigureServices(WebApplicationBuilder builder)
        {
            var services = builder.Services;
            var configuration = builder.Configuration;

            // ---- Options, validated at startup ------------------------------------------------
            // ValidateOnStart turns a missing signing key into an immediate startup failure with a
            // clear message, rather than a confusing 500 on the first login attempt.
            services.AddOptions<MoodTrackerOptions>()
                .Bind(configuration.GetSection(MoodTrackerOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<AdminAuthOptions>()
                .Bind(configuration.GetSection(AdminAuthOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var adminAuthOptions = configuration
                .GetSection(AdminAuthOptions.SectionName)
                .Get<AdminAuthOptions>() ?? new AdminAuthOptions();

            // ---- Persistence -------------------------------------------------------------------
            var connectionString = configuration.GetConnectionString("MySQLConnectionString")
                ?? throw new InvalidOperationException(
                    "Connection string 'MySQLConnectionString' is not configured.");

            services.AddDbContext<MoodTrackerDbContext>(options =>
                options.UseMySQL(connectionString));

            services.AddScoped<DatabaseInitialiser>();

            // ---- Application services ----------------------------------------------------------
            services.AddHttpContextAccessor();
            services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
            services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
            services.AddScoped<IUserKeyProvider, CookieUserKeyProvider>();
            services.AddScoped<IMoodService, MoodService>();
            services.AddScoped<IAdminAuthService, AdminAuthService>();

            // ---- Authentication ----------------------------------------------------------------
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = adminAuthOptions.Issuer,
                        ValidateAudience = true,
                        ValidAudience = adminAuthOptions.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(adminAuthOptions.JwtSigningKey)),
                        ValidateLifetime = true,

                        // The default five minutes of clock skew is unnecessary here: issuer and
                        // validator are the same process, so an expired token should be rejected
                        // when it expires.
                        ClockSkew = TimeSpan.Zero
                    };
                });

            services.AddAuthorization();

            // ---- CORS --------------------------------------------------------------------------
            var allowedOrigins = configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? new[] { "http://localhost:4200" };

            services.AddCors(options => options.AddPolicy(AngularCorsPolicy, policy =>
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      // Required so the browser sends and stores the anonymous identity cookie on
                      // cross-origin requests from the Angular app.
                      .AllowCredentials()));

            // ---- MVC and Swagger ---------------------------------------------------------------
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Mood Tracker API",
                    Version = "v1",
                    Description = "Tracks a team's daily mood."
                });

                // Lets the admin endpoints be exercised from the Swagger UI with a bearer token.
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Paste the token returned by POST /api/auth/login."
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }] = Array.Empty<string>()
                });

                var xmlPath = Path.Combine(
                    AppContext.BaseDirectory,
                    $"{typeof(Program).Assembly.GetName().Name}.xml");

                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            });

            services.AddHealthChecks();
        }

        private static async Task ConfigurePipelineAsync(WebApplication app)
        {
            // Swagger is exposed in every environment on purpose: it is the quickest way for a
            // reviewer running the container to see and exercise the API. A real production
            // deployment would gate this behind an environment check.
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mood Tracker API v1");
            });

            // Returns RFC 9457 problem details for unhandled exceptions instead of a bare 500 with
            // a stack trace.
            app.UseExceptionHandler(new ExceptionHandlerOptions
            {
                AllowStatusCode404Response = true,
                ExceptionHandler = ProblemDetailsExceptionHandler.HandleAsync
            });

            // NOTE: the template called UseHttpsRedirection here. Inside Docker the container only
            // serves HTTP on 8080 with no certificate bound to 8081, so redirecting would break
            // every API call from the Angular client. TLS is terminated upstream in a real
            // deployment; this is documented in the README.

            app.UseCors(AngularCorsPolicy);

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapHealthChecks("/health");

            await InitialiseDatabaseAsync(app);
        }

        private static async Task InitialiseDatabaseAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();

            var initialiser = scope.ServiceProvider.GetRequiredService<DatabaseInitialiser>();

            await initialiser.InitialiseAsync();
        }
    }
}
