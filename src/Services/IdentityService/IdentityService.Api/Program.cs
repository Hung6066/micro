using His.Hope.IdentityService.Api.Composition;
using His.Hope.Infrastructure;
using His.Hope.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddIdentityService();

var app = builder.Build();
app.UseIdentityServicePipeline();

if (builder.Configuration.GetValue("Persistence:MigrationOnly", false))
{
    return;
}

app.MapIdentityServiceEndpoints();
app.MapHisHopeHealthEndpoints();
app.Run();

public partial class Program { }
