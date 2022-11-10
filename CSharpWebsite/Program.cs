using CSharpWebsite;
using CSharpWebsite.Content.Database;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;

Controller.Init();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews(); 
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromSeconds(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSession();
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

#region Errors
app.MapGet("/banned", async (HttpContext context) =>
{
    await FileServerMiddleware.ReplyFile(context, "Content/Pages/banned.html");
});
List<Tuple<int, string>> ServerStorage = new List<Tuple<int, string>>();
app.MapGet("/submitLogin", async (HttpContext context, string email, string password) =>
{
    if (email == "None" || password == "None") { await context.Response.WriteAsync("[Failure] (Invalid Input)"); return; }
    var getUser = await WebsiteSchema.Get(email);
    if (getUser == null) { await context.Response.WriteAsync("[Failure] (Not Registered)"); return; }
    if (getUser.IsLocked) { await context.Response.WriteAsync("[Failure] (Account Locked!)"); return; }
    if (getUser.IsBanned) { await context.Response.WriteAsync("[Failure] (Account Banned!)"); return; }
    var decryptedEmail = DataEncryption.Decrypt(getUser.Email, getUser.EnryptionKey);
    var decryptPassword = DataEncryption.Decrypt(getUser.Password, getUser.EnryptionKey);
    if (decryptPassword != password) { await context.Response.WriteAsync("[Failure] (Incorrect Password)"); return; }
    var userIndex = new Random().Next(int.MinValue, int.MaxValue);
    if (!ServerStorage.Any(s => s.Item2.ToLower() == decryptedEmail.ToLower()))
    {
        ServerStorage.Add(Tuple.Create(userIndex, decryptedEmail));
    } else
    {
        ServerStorage[ServerStorage.FindIndex(s => s.Item2.ToLower() == decryptedEmail.ToLower())] = Tuple.Create(userIndex, decryptedEmail);
    }
    await context.Response.WriteAsync("[Success] (Logged In) " + JsonSerializer.Serialize(new UserResponse() { email = decryptedEmail,sessionId= userIndex,URLThumbnail=getUser.URLThumbnail }));
    return;
});
app.MapGet("/createAccount", async (HttpContext context, string email, string password) =>
{
    if (email == "None" || password == "None") { await context.Response.WriteAsync("[Failure] (Invalid Input)"); return; }
    var getUser = await WebsiteSchema.Get(email);
    if (getUser != null) { await context.Response.WriteAsync("[Failure] (Already Registered)"); return; }
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
        await context.Response.WriteAsync("[Success] (Account Created)");
    }
    else
    {
        await context.Response.WriteAsync("[Failure] (Failed To Create Account!)");
    }
    return;
});
#endregion
app.MapGet("/verifyLogin", async (HttpContext context, string json) =>
{
    var jsonResult = JsonSerializer.Deserialize<UserResponse>(json);
    if (jsonResult != null)
    {
        var element = ServerStorage.Find(s => s.Item1 == jsonResult.sessionId);
        if (element != null && jsonResult != null)
        {
            if (element.Item2.ToLower() == jsonResult.email.ToLower())
            {
                await context.Response.WriteAsync("Verified");
            }
            else
            {
                var gu = await WebsiteSchema.Get(element.Item2);
                if (gu != null)
                {
                    gu.IsBanned = true;
                    await gu.Update();
                }
                await context.Response.WriteAsync("InvalidMove");
            }
        }
        else
        {
            await context.Response.WriteAsync("NotLoggedIn");
        }
    } else
    {
        await context.Response.WriteAsync("NotLoggedIn");
    }
    return;
});

app.MapDefaultControllerRoute();
app.MapRazorPages();

app.Run();

public class UserResponse
{
    public string email { get; set; }
    public int sessionId { get; set; }
    public string URLThumbnail { get; set; }
}