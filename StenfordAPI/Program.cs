using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Stenford.Common.Utility;
using Stenford.Domain.DataContext;
using Stenford.Service.Account;
using Stenford.Service.Dashboard;
using Stenford.Service.Dropdown;
using Stenford.Service.JwtToken;
using Stenford.Service.Report;
using Stenford.Service.SalesPerson;
using Stenford.Service.Showroom;
using Stenford.Service.Visit;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddTransient<IJwtTokenRepository, JwtTokenRepository>();
builder.Services.AddTransient<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ISalesPersonRepository, SalesPersonRepository>();
builder.Services.AddScoped<IDropdownRepository, DropdownRepository>();
builder.Services.AddScoped<IShowroomRepository, ShowroomRepository>();
builder.Services.AddScoped<IVisitRepository, VisitRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Description =
            "\n**Admin Token:**\n" +
            "```\n" +
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9lbWFpbGFkZHJlc3MiOiJzdXBlci5hZG1pbkBzdGFuZm9yZC5jb20iLCJBc3BOZXRVc2VySUQiOiIxMTExMTExMS0xMTExLTExMTEtMTExMS0xMTExMTExMTExMTEiLCJSb2xlSWQiOiIxIiwiUm9sZU5hbWUiOiJBZG1pbiIsIkFkbWluSUQiOiIyIiwiU2FsZXNQZXJzb25JRCI6IjAiLCJBc3NvY2lhdGVJRCI6IjIiLCJBc3BOZXRVc2VyV2lzZVJvbGVJRCI6IjEiLCJVc2VyTmFtZSI6IlN1cGVyIEFkbWluIiwiaHR0cDovL3NjaGVtYXMueG1sc29hcC5vcmcvd3MvMjAwNS8wNS9pZGVudGl0eS9jbGFpbXMvbmFtZWlkZW50aWZpZXIiOiJzdXBlci5hZG1pbkBzdGFuZm9yZC5jb20iLCJleHAiOjI2NTAzNTYwODUsImlzcyI6IlRlc3QuY29tIiwiYXVkIjoiQXVkaWVuY2UifQ.sp8-UVAcHisbzuJFk767dkTCSumFgIOzhWnsiDy0OCY\n" +
            "```\n\n" +
            "**SalesPerson Token:**\n" +
            "```\n" +
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9lbWFpbGFkZHJlc3MiOiJwcml5YUBzdGFuZm9yZC5jb20iLCJBc3BOZXRVc2VySUQiOiIzMzMzMzMzMy0zMzMzLTMzMzMtMzMzMy0zMzMzMzMzMzMzMzMiLCJSb2xlSWQiOiIyIiwiUm9sZU5hbWUiOiJTYWxlc1BlcnNvbiIsIkFkbWluSUQiOiIwIiwiU2FsZXNQZXJzb25JRCI6IjIiLCJBc3NvY2lhdGVJRCI6IjIiLCJBc3BOZXRVc2VyV2lzZVJvbGVJRCI6IjMiLCJVc2VyTmFtZSI6IlByaXlhIFNoYXJtYSIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL25hbWVpZGVudGlmaWVyIjoicHJpeWFAc3RhbmZvcmQuY29tIiwiZXhwIjoyNjUwMzU2NTY3LCJpc3MiOiJUZXN0LmNvbSIsImF1ZCI6IkF1ZGllbmNlIn0.39jVDwOcPJP31-I8NF9w-WW0e2Fh5PaOIn86DnJMVaY\n" +
            "```",
        Title = "Stenford API",
        Version = "v1"
    });
});
builder.Services.AddSwaggerGen(c =>
{
    // Add Bearer Token Authentication option
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token.\r\nExample: \"Bearer abc123xyz\""
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
            new string[] {}
        }
    });
});

//default authentication scheme------------------------------------
var jwtIssuer = builder.Configuration.GetSection("Jwt:Issuer").Get<string>();
var jwtKey = builder.Configuration.GetSection("Jwt:Key").Get<string>();
var jwtAudience = builder.Configuration.GetSection("Jwt:Audience").Get<string>();
if (jwtKey != null)
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
}
//default scheme ends----------------------------------------------

builder.Services.AddHttpClient<IMapLinkParser, GoogleMapLinkParser>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });


var app = builder.Build();

//only for dev-purpose
// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();