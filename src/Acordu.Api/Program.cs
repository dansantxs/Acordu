using Acordu.Api.Composicao;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAcorduApi(builder.Configuration);

var app = builder.Build();
_ = app.UseAcorduApi(); // endpoints de negócio entram nos próximos PRs do marco
app.Run();

/// <summary>Exposto para os testes funcionais (WebApplicationFactory).</summary>
public partial class Program;
