using CSharpWebsite;
using CSharpWebsite.Content.Database;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;

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
app.MapGet("/", async (HttpContext context) =>
{
    await FileServerMiddleware.ReplyFile(context, "Content/Pages/index.html");
});
app.MapPost("/", async (HttpContext context) =>
{
    await FileServerMiddleware.ReplyFile(context, "Content/Pages/index.html");
});
#endregion

#region QuoteController
var quoteIndex = 0;
app.MapGet("/randomizeQuote", async (HttpContext context) =>
{
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
app.MapGet("/submitLogin", async (HttpContext context, string email, string password) =>
{
    Console.WriteLine($"Checking Login Info: {email} -> {password}");
    if (email == "None" || password == "None") { await context.Response.WriteAsync("Invalid Input"); return; }
    var getUser = await WebsiteSchema.Get(email);
    if (getUser == null) { await context.Response.WriteAsync("Not Registered"); return; }
    var decryptedEmail = DataEncryption.Decrypt(getUser.Email, getUser.EnryptionKey);
    var decryptPassword = DataEncryption.Decrypt(getUser.Password, getUser.EnryptionKey);
    if (decryptPassword != password) { await context.Response.WriteAsync("Incorrect Password"); return; }
    await context.Response.WriteAsync(JsonSerializer.Serialize(new UserResponse() { email = decryptedEmail }));
    return;
});
app.MapGet("/createAccount", async (HttpContext context, string email, string password) =>
{
    if (email == "None" || password == "None") { await context.Response.WriteAsync("[Failure] Invalid Input"); return; }
    var getUser = await WebsiteSchema.Get(email);
    if (getUser != null) { Console.WriteLine("Found Account"); await context.Response.WriteAsync("[Failure] Already Registered"); return; }
    var keys = DataEncryption.RandomKeyString(new Random().Next(5, 6));
    var encryptedEmail = DataEncryption.Encrypt(email, keys);
    var encryptedPassword = DataEncryption.Encrypt(password, keys);
    var data = new WebsiteSchema()
    {
        Email = encryptedEmail,
        Password = encryptedPassword,
        EnryptionKey = keys,
        DiscordId = null,
        IsLocked = false,
        IsPaired = false,
        LoginAttempts = 0,
        PermissionLevel = 0,
        _id = new Random().Next(int.MaxValue)
    };
    var res = await data.Upload();
    if (res)
    {
        await context.Response.WriteAsync("[Success] Account Created");
    }
    else
    {
        await context.Response.WriteAsync("[Failure] Failed To Create Account!");
    }
    return;
});
#endregion

app.MapDefaultControllerRoute();
app.MapRazorPages();

app.Run();

public class UserResponse
{
    public string email { get; set; }
}