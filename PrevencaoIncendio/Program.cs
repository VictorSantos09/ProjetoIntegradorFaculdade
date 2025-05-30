using MongoDB.Driver;
using PrevencaoIncendio;
using PrevencaoIncendio.Components;
using PrevencaoIncendio.Data;
using PrevencaoIncendio.Repositories;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<IMongoClient>(sp => new MongoClient(DbManager.ConnectionString));
builder.Services.AddTransient(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(DbManager.DatabaseName);
});

builder.Services.AddTransient<IValoresRepository, ValoresRepository>();
builder.Services.AddHostedService<MqttWorker>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRadzenComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();