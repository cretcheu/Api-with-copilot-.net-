using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using UserManagementAPI.Middleware;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace UserManagementAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            // Configure authentication
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = Configuration["Jwt:Issuer"],
                        ValidAudience = Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:Key"]))
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            context.Response.StatusCode = 401;
                            context.Response.ContentType = "application/json";
                            var result = JsonSerializer.Serialize(new { message = "Authentication failed" });
                            return context.Response.WriteAsync(result);
                        },
                        OnTokenValidated = context =>
                        {
                            // Log successful token validation
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Startup>>();
                            logger.LogInformation("Token validated successfully.");
                            return Task.CompletedTask;
                        }
                    };
                });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            // Use the TokenValidationMiddleware
            app.UseMiddleware<TokenValidationMiddleware>();

            // Middleware to log requests and responses
            app.Use(async (context, next) =>
            {
                // Log request
                context.Request.EnableBuffering();
                var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
                context.Request.Body.Position = 0;
                var requestLog = $"Incoming Request: {context.Request.Method} {context.Request.Path} {requestBody}";
                logger.LogInformation(requestLog);
                await AppendLogAsync(requestLog);

                // Log response
                var originalBodyStream = context.Response.Body;
                using (var responseBody = new MemoryStream())
                {
                    context.Response.Body = responseBody;
                    await next.Invoke();
                    context.Response.Body.Seek(0, SeekOrigin.Begin);
                    var responseBodyText = await new StreamReader(context.Response.Body).ReadToEndAsync();
                    context.Response.Body.Seek(0, SeekOrigin.Begin);
                    var responseLog = $"Outgoing Response: {context.Response.StatusCode} {responseBodyText}";
                    logger.LogInformation(responseLog);
                    await AppendLogAsync(responseLog);
                    await responseBody.CopyToAsync(originalBodyStream);
                }
            });

            // Middleware to handle exceptions and log them
            app.Use(async (context, next) =>
            {
                try
                {
                    await next.Invoke();
                }
                catch (System.Exception ex)
                {
                    var errorLog = $"An unhandled exception has occurred: {ex.Message}";
                    logger.LogError(ex, errorLog);
                    // Log the error to a file
                    await AppendLogAsync(errorLog);
                    context.Response.ContentType = "application/json";
                    var errorResponse = new { Message = "An error occurred while processing your request." };
                    await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                }
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        // Helper method to append logs to a file
        private async Task AppendLogAsync(string log)
        {
            const string logFilePath = "logs.txt";
            // Limit the maximum number of logs to 1000
            const int maxLogLines = 1000;

            var logLines = new List<string>();
            if (File.Exists(logFilePath))
            {
                logLines = (await File.ReadAllLinesAsync(logFilePath)).ToList();
            }

            logLines.Add(log);
            if (logLines.Count > maxLogLines)
            {
                logLines = logLines.Skip(logLines.Count - maxLogLines).ToList();
            }

            await File.WriteAllLinesAsync(logFilePath, logLines);
        }
    }
}
