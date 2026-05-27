using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using AskEiva.WebUI.Components;
using AskEiva.WebUI.Components.Account;
using AskEiva.Domain.Entities; 
using AskEiva.Domain.Repositories;
using AskEiva.Domain.Services;
using AskEiva.Infrastructure.Repositories; 
using AskEiva.Infrastructure.Services;
using MudBlazor.Services;

// Change back to WebApplication to support Blazor Server UI pipelines
var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURATION RETRIEVAL ---
string domain = builder.Configuration["FRESHDESK_DOMAIN"] ?? "eiva";
string apiKey = builder.Configuration["FRESHDESK_API_KEY"] ?? "YOUR_KEY";
string weaviateUrl = builder.Configuration["WEAVIATE_URL"] ?? "https://your-cluster.weaviate.network";
string weaviateKey = builder.Configuration["WEAVIATE_API_KEY"] ?? "YOUR_KEY";
string mistralKey = builder.Configuration["MISTRAL_API_KEY"] ?? "YOUR_KEY";

if (!weaviateUrl.StartsWith("http://") && !weaviateUrl.StartsWith("https://"))
{
    weaviateUrl = $"https://{weaviateUrl.Trim('\"', ' ')}";
}
else
{
    weaviateUrl = weaviateUrl.Trim('\"', ' ');
}

string cleanApiKey = weaviateKey.Trim('\"', ' ');
string cleanMistralKey = mistralKey.Trim('\"', ' ');
var freshdeskAuthToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{apiKey}:X"));

// 💡 NEW CROSS-COMPATIBILITY PROXY GATEWAY: Handles campus firewall SSL decryption layers seamlessly
// 💡 SECURE ENVIRONMENT DETECTION GATEWAY
var targetSystemProxy = System.Net.Http.HttpClient.DefaultProxy;
// Force true if running under your AU work machine domain profile path string
bool isWorkNetworkActive = (targetSystemProxy != null && targetSystemProxy.GetProxy(new Uri("https://uznwxkhmqa6krcdebigw.c0.europe-west3.gcp.weaviate.cloud/")) != null) 
                          || AppDomain.CurrentDomain.BaseDirectory.Contains("au667198", StringComparison.OrdinalIgnoreCase);

SocketsHttpHandler GetNetworkHandlerForEnvironment()
{
    var handler = new SocketsHttpHandler
    {
        Proxy = targetSystemProxy,
        UseProxy = true
    };

    // If on the work machine/network, always force-bypass intercepted certificate challenge exceptions
    if (isWorkNetworkActive)
    {
        handler.SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
        };
    }

    return handler;
}

// --- 2. SYSTEM CORE SERVICES & BLAZOR ENGINE ---
builder.Services.AddMudServices();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MediatR scanning your Application layer query context assemblies
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AskEiva.Application.Knowledge.Queries.SearchKnowledgeQuery).Assembly));

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// --- 3. HTTP CLIENTS & INTEGRATIONS (HTTP BOUND REPOSITORIES) ---

// Freshdesk Ticket Integration
builder.Services.AddHttpClient<IFreshdeskService, FreshdeskService>(client =>
{
    client.BaseAddress = new Uri($"https://{domain}.freshdesk.com/api/v2/");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", freshdeskAuthToken);
})
.ConfigurePrimaryHttpMessageHandler(() => GetNetworkHandlerForEnvironment());

// Solutions Manual Crawler
builder.Services.AddHttpClient<IDocumentationCrawler, DocumentationCrawler>(client =>
{
    client.BaseAddress = new Uri($"https://{domain}.freshdesk.com/api/v2/");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", freshdeskAuthToken);
})
.ConfigurePrimaryHttpMessageHandler(() => GetNetworkHandlerForEnvironment());

// Weaviate Identity User Management
builder.Services.AddHttpClient<UserRepository>(client =>
{
    client.BaseAddress = new Uri(weaviateUrl);
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cleanApiKey}");
    client.DefaultRequestHeaders.Add("X-Weaviate-Api-Key", cleanApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => GetNetworkHandlerForEnvironment());

// Automated Weaviate Schema Provisioner Worker Link
builder.Services.AddHttpClient<WeaviateSchemaProvisioner>(client =>
{
    client.BaseAddress = new Uri(weaviateUrl);
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cleanApiKey}");
    client.DefaultRequestHeaders.Add("X-Weaviate-Api-Key", cleanApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => GetNetworkHandlerForEnvironment());

// Ticket Repository Client 
builder.Services.AddHttpClient<ITicketRepository, TicketRepository>(client =>
{
    client.BaseAddress = new Uri(weaviateUrl);
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cleanApiKey}");
    client.DefaultRequestHeaders.Add("X-Weaviate-Api-Key", cleanApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => GetNetworkHandlerForEnvironment());

// The Software Release Ingestion Scraper Architecture Pipeline
builder.Services.AddHttpClient<AskEiva.Domain.Services.IReleaseNotesScraper, AskEiva.Infrastructure.Services.ReleaseNotesScraper>(client =>
{
    // Point to the explicit domain root
    client.BaseAddress = new Uri("https://download.eiva.com/#");
    
    // Clear out any old defaults to prevent header collisions
    client.DefaultRequestHeaders.Clear();
    
    // 🌐 Emulate a mainstream browser footprint
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
    client.DefaultRequestHeaders.Add("Connection", "keep-alive");
})
.ConfigurePrimaryHttpMessageHandler(() => GetNetworkHandlerForEnvironment());

// GraphRAG Semantic Relations Repository Client
builder.Services.AddHttpClient<IGraphRepository, GraphRepository>(client =>
{
    client.BaseAddress = new Uri(weaviateUrl);
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cleanApiKey}");
    client.DefaultRequestHeaders.Add("X-Weaviate-Api-Key", cleanApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => GetNetworkHandlerForEnvironment());

// Documentation Manuals Repository Client
builder.Services.AddHttpClient<IDocumentationRepository, DocumentationRepository>(client =>
{
    client.BaseAddress = new Uri(weaviateUrl);
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cleanApiKey}");
    client.DefaultRequestHeaders.Add("X-Weaviate-Api-Key", cleanApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => GetNetworkHandlerForEnvironment());

// Multi-Source Hybrid Retrieval Repository (💡 DUAL AUTH FIXED)
builder.Services.AddHttpClient<IKnowledgeRetrievalRepository, KnowledgeRetrievalRepository>(client =>
{
    client.BaseAddress = new Uri(weaviateUrl);
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cleanApiKey}");
    client.DefaultRequestHeaders.Add("X-Weaviate-Api-Key", cleanApiKey);
    
    // 💡 THE CRITICAL FIX: Forwards your Mistral token to Weaviate for on-the-fly embedding text vectorization!
    client.DefaultRequestHeaders.Add("X-Mistral-Api-Key", cleanMistralKey);
})
.ConfigurePrimaryHttpMessageHandler(() => GetNetworkHandlerForEnvironment());

// Mistral Conversational Inference Client
builder.Services.AddHttpClient<IMistralChatService, MistralChatService>(client =>
{
    client.BaseAddress = new Uri("https://api.mistral.ai/");
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cleanMistralKey}");
})
.ConfigurePrimaryHttpMessageHandler(() => GetNetworkHandlerForEnvironment());

// Mistral Graph Extraction Engine Client
builder.Services.AddHttpClient<IExtractionEngine, MistralExtractionEngine>(client =>
{
    client.BaseAddress = new Uri("https://api.mistral.ai/");
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cleanMistralKey}");
})
.ConfigurePrimaryHttpMessageHandler(() => GetNetworkHandlerForEnvironment());

// --- 4. ASP.NET IDENTITY LIFECYCLE MANAGEMENT BINDINGS ---
builder.Services.AddScoped<IUserStore<ApplicationUser>, WeaviateUserStore>();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

// --- 5. HTTP REQUEST PIPELINE (MIDDLEWARE) ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

app.MapAdditionalIdentityEndpoints();
app.MapCustomLogoutEndpoint();

// --- 6. ASYNCHRONOUS STARTUP PROVISIONING HOOK ---
using (var scope = app.Services.CreateScope())
{
    var provisioner = scope.ServiceProvider.GetRequiredService<WeaviateSchemaProvisioner>();
    await provisioner.EnsureSchemaAsync();
}

app.Run();