using LH.Main.Contracts;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new HealthStatusResponse("ok")));
app.MapGet("/health/ready", () => Results.Ok(new HealthStatusResponse("ok")));

app.Run();

public partial class Program;
