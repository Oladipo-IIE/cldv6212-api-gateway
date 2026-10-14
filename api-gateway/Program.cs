
using Azure.Data.Tables;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace api_gateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            //add swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();


            var ConnectionString = builder.Configuration.GetConnectionString("AzureStorage");

            builder.Services.AddSingleton(sp =>
            {
                var tableClient = new TableClient(
                    ConnectionString,
                    "Users");

                tableClient.CreateIfNotExists();

                return tableClient;
            });

            //add and configure authentication middleware
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
             .AddJwtBearer(options =>
             {
                 options.TokenValidationParameters = new TokenValidationParameters
                 {
                     ValidateIssuer = true,
                     ValidateAudience = true,
                     ValidateLifetime = true,
                     ValidateIssuerSigningKey = true,

                     ValidIssuer = builder.Configuration["Jwt:Issuer"],
                     ValidAudience = builder.Configuration["Jwt:Audience"],

                     IssuerSigningKey = new SymmetricSecurityKey(
                         Encoding.UTF8.GetBytes(
                             builder.Configuration["Jwt:Key"]!))
                 };
             });

            //add authorisation middleware
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("authenticated", policy =>
                {
                    policy.RequireAuthenticatedUser();
                });
            });

            //register yarp
            builder.Services.AddReverseProxy()
                .LoadFromConfig(
                 builder.Configuration.GetSection("ReverseProxy"));

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            //In the next few lines we activate the various middleware
            //activate https middleware
            app.UseHttpsRedirection();

            //activate authentication and authorisation middleware
            app.UseAuthentication();
            app.UseAuthorization();

            //use swagger middleware
            app.UseSwagger();

            app.UseSwaggerUI(options =>
            {
                // Gateway's own API documentation
                options.SwaggerEndpoint(
                    "/swagger/v1/swagger.json",
                    "API Gateway");

                // Notes microservice documentation
                options.SwaggerEndpoint(
                    "/docs/notes/openapi.json",
                    "Notes Microservice");
            });


            app.MapControllers();

            // Map YARP routes
            app.MapReverseProxy();


            app.Run();
        }
    }
}
