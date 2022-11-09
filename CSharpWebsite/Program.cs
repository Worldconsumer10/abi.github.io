using CSharpWebsite;
using CSharpWebsite.Content.Database;
using Microsoft.Extensions.FileProviders;

Controller.Init();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Environment.CurrentDirectory, "Content/ImageAssets")),
    RequestPath = "/Images"
});
app.UseAuthorization();

#region Index
app.MapGet("/",async (HttpContext context)=>{
    await FileServerMiddleware.ReplyFile(context, "Content/Pages/index.html");
});
app.MapPost("/", async (HttpContext context) => {
    await FileServerMiddleware.ReplyFile(context, "Content/Pages/index.html");
});
#endregion

#region QuoteController
var quoteIndex = 0;
app.MapGet("/randomizeQuote", async (HttpContext context) => {
    var files = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "Content/ImageAssets/quotes"));
    quoteIndex = new Random().Next(files.Length);
    var file = files[quoteIndex];
    if (file != null)
    {
        return "Images/quotes/" + Path.GetFileName(file);
    }
    return "Images/quotes/fuckingledgend.png";
});
app.MapGet("/nextQuote", async (HttpContext context) =>
{
    var files = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "Content/ImageAssets/quotes"));
    quoteIndex++;
    if (quoteIndex > files.Length) { quoteIndex = 0; }
    var file = files[quoteIndex];
    if (file != null)
    {
        return "Images/quotes/" + Path.GetFileName(file);
    }
    return "Images/quotes/fuckingledgend.png";
});
app.MapGet("/previousQuote", async (HttpContext context) =>
{
    var files = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "Content/ImageAssets/quotes"));
    quoteIndex--;
    if (quoteIndex < 0) { quoteIndex = files.Length; }
    var file = files[quoteIndex];
    if (file != null)
    {
        return "Images/quotes/" + Path.GetFileName(file);
    }
    return "Images/quotes/fuckingledgend.png";
});
#endregion

#region LoginHandler
app.MapPost("/login", async (HttpContext context) =>
{
    await FileServerMiddleware.ReplyFile(context, "Content/Pages/login.html");
});
app.MapPost("/signUp", async (HttpContext context) =>
{
    await FileServerMiddleware.ReplyFile(context, "Content/Pages/signup.html");
});
app.MapGet("/submitLogin", async (HttpContext context) =>
{

});
app.MapGet("/createAccount", async (HttpContext context) =>
{

});
#endregion

app.MapDefaultControllerRoute();
app.MapRazorPages();

app.Run();