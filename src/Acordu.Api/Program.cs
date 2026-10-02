var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

/// <summary>Exposto para os testes funcionais (WebApplicationFactory).</summary>
public partial class Program;
