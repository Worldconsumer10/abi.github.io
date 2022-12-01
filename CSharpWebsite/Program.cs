using AnniUpdate.Database;
using CSharpWebsite;
using CSharpWebsite.Content.Database;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.Net;
using System.Net.Mail;
using System.Text;
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
    var file = files[0];
    try
    {
        file = files[quoteIndex];
    }
    catch (Exception) { }
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
#endregion

#region AccountRelated
List<Tuple<int, string>> ServerStorage = new List<Tuple<int, string>>();
app.MapGet("/submitLogin", async (HttpContext context, string email, string password) =>
{
    if (email == "None" || password == "None") { await ContextResponse.RespondAsync(context.Response,"[Failure] (Invalid Input)"); return; }
    var getUser = await WebsiteSchema.Get(email);
    if (getUser == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Not Registered)"); return; }
    if (getUser.IsLocked) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Account Locked!)"); return; }
    if (getUser.IsBanned) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Account Banned!)"); return; }
    if (getUser.lockDate != DateTime.MaxValue) { await ContextResponse.RespondAsync(context.Response,"[Failure] (Account Locked! Try Again Later!)"); return; }
    var decryptedEmail = DataEncryption.Decrypt(getUser.Email, getUser.EnryptionKey);
    var decryptPassword = DataEncryption.Decrypt(getUser.Password, getUser.EnryptionKey);
    if (decryptPassword != password)
    {
        if (getUser.lockRetries <= 0)
        {
            var keys = DataEncryption.GetRandomString(5);
            getUser.resetCode = keys[new Random().Next(keys.Count)];
            sendResetEmail(email, getUser.resetCode);
            await ContextResponse.RespondAsync(context.Response,$"[Failure] (Incorrect Password. Resending Confirmation Email)");
        }
        else
        {
            getUser.lockRetries--;
            var keys = DataEncryption.GetRandomString(5);
            getUser.resetCode = keys[new Random().Next(keys.Count)];
            sendResetEmail(email, getUser.resetCode);
            await ContextResponse.RespondAsync(context.Response,$"[Failure] (Incorrect Password. {getUser.lockRetries} Retries Left)");
        }
        await getUser.Update();
        return;
    }
    var userIndex = new Random().Next(int.MinValue, int.MaxValue);
    if (!ServerStorage.Any(s => s.Item2.ToLower() == decryptedEmail.ToLower()))
    {
        ServerStorage.Add(Tuple.Create(userIndex, decryptedEmail));
    }
    else
    {
        ServerStorage[ServerStorage.FindIndex(s => s.Item2.ToLower() == decryptedEmail.ToLower())] = Tuple.Create(userIndex, decryptedEmail);
    }
    var highestpermlist = getUser.permissionLevel;
    highestpermlist.Sort((a, b) => { if (a.userLevel < b.userLevel) return 1; if (a.userLevel > b.userLevel) return -1; return 0; });
    var highestperm = 0;
    if (highestpermlist.Count > 0)
    {
        highestperm = highestpermlist[0].userLevel;
    }
    await ContextResponse.RespondAsync(context.Response,"[Success] (Logged In) " + JsonSerializer.Serialize(new UserResponse() { email = decryptedEmail, sessionId = userIndex, URLThumbnail = getUser.URLThumbnail, permissionLevel = highestperm }));
    return;
});
app.MapGet("/emailverification", async (HttpContext context, string code) =>
{
    var user = (await WebsiteSchema.GetAll()).Find(c => c.resetCode == code);
    if (user == null) { await ContextResponse.RespondAsync(context.Response,"[Failure] Invalid Code"); return; }
    user.lockDate = DateTime.MaxValue;
    user.lockRetries = 3;
    user.resetCode = null;
    await user.Update();
    await ContextResponse.RespondAsync(context.Response,"[Success] Account Unlocked!");
});
void sendResetEmail(string email, string code)
{
    string SMTPServer = "smtp.gmail.com";
    int SMTP_Port = 587;
    string From = File.ReadLines(Path.Combine(Directory.GetCurrentDirectory(), "config.txt")).ElementAt(0);
    string Password = File.ReadLines(Path.Combine(Directory.GetCurrentDirectory(), "config.txt")).ElementAt(1);
    Console.WriteLine(File.ReadLines(Path.Combine(Directory.GetCurrentDirectory(), "config.txt")).ElementAt(1));
    if (Password == "nocode") return;
    string To = email;
    string Subject = "Account Access Locked";
    string ResetLink = $"https://localhost:7049/emailverification?code={code}";
    string Body = $"Dear {email}\nRecent activity on your account for website:<br/><br/>ubunifuserver.com<br/><br/>Has been marked as suspicious! Please follow this link:<br/>{ResetLink}<br/>Or use the code: {code}<br/>When attempting to login next!<br/><br/><br/><i>If you did not try logging in recently it is suggested to keep an eye on your account as someone may be attempting to access it!</i><br/><br/>This is an automated message! Do not reply!";
    var smtpClient = new SmtpClient(SMTPServer, SMTP_Port)
    {
        DeliveryMethod = SmtpDeliveryMethod.Network,
        UseDefaultCredentials = false,
        EnableSsl = true
    };
    smtpClient.Credentials = new NetworkCredential(From, Password); //Use the new password, generated from google!
    var message = new MailMessage(new MailAddress(From, "Anni"), new System.Net.Mail.MailAddress(To, To));
    message.Subject = Subject;
    message.IsBodyHtml = true;
    message.Body = Body;
    smtpClient.Send(message);
    Console.WriteLine($"Sent Email To: {email}");
}
int requiredServer = 3;
app.MapPost("/sendConfigure", async (HttpContext context, string email, string id) =>
{
    try
    {
        var idi = int.Parse(id);
        var storage = ServerStorage.Find(t => t.Item1 == idi && t.Item2 == email);
        if (storage == null) { await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
        var user = await WebsiteSchema.Get(storage.Item2);
        if (user == null) { await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
        if (!user.permissionLevel.Any(l => l.userLevel >= requiredServer)) { await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
        await FileServerMiddleware.ReplyFile(context, "Content/Pages/serverconfiglist.html");
        return;
    }
    catch (Exception)
    {
        await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return;
    }
});
app.MapGet("/requestServers", async (HttpContext context, string email, string id) =>
{
    try
    {
        var idi = int.Parse(id);
        var storage = ServerStorage.Find(t => t.Item1 == idi && t.Item2 == email);
        if (storage == null) { await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
        var user = await WebsiteSchema.Get(storage.Item2);
        if (user == null) { await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
        List<ServerOverviewList> lsits = new List<ServerOverviewList>();
        foreach (var server in user.permissionLevel.Where(u => u.userLevel >= requiredServer))
        {
            if (server == null) continue;
            lsits.Add(new ServerOverviewList()
            {
                server = server,
                Name = (await GuildServer.Get(server.Id))?.GuildName ?? "Unknown Guild"
            });
        }
        await ContextResponse.RespondAsync(context.Response,JsonSerializer.Serialize(lsits));
    }
    catch (Exception)
    {
        await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return;
    }
});
app.MapGet("/getServerDetails", async (HttpContext context, string id) =>
{
    try
    {
        ulong idd = ulong.Parse(id);
        var server = await GuildServer.Get(idd);
        if (server == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Server Does Not Exist!)"); return; }
        await ContextResponse.RespondAsync(context.Response, "[Success] " + JsonSerializer.Serialize(server));
    }
    catch (Exception e) { Console.WriteLine($"{e.Message}\n{e.InnerException}\n\n{e.StackTrace}"); await ContextResponse.RespondAsync(context.Response, "[Failure] (An Error Occured)"); return; }
});
app.MapGet("/updateModule", async (HttpContext context, string id, string moduleName, string state) =>
{
    try
    {
        ulong idd = ulong.Parse(id);
        var server = await GuildServer.Get(idd);
        if (server == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Server Does Not Exist!)"); return; }
        var module = server.eventModules.Find(m => m.Name == moduleName);
        if (module == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Module Does Not Exist!)"); return; }
        module.enabled = bool.Parse(state);
        server.eventModules[server.eventModules.FindIndex(m => m.Name == moduleName)] = module;
        await server.UpdateOne();
        await ContextResponse.RespondAsync(context.Response, "[Success] (Module Updated)");
    }
    catch (Exception e) { Console.WriteLine($"{e.Message}\n{e.InnerException}\n\n{e.StackTrace}"); await ContextResponse.RespondAsync(context.Response, "[Failure] (An Error Occured)"); return; }
});
app.MapGet("/updateCommand", async (HttpContext context, string id, string commandname, string state) =>
{
    try
    {
        ulong idd = ulong.Parse(id);
        var server = await GuildServer.Get(idd);
        if (server == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Server Does Not Exist!)"); return; }
        var module = server.commands.Find(m => m.command_name == commandname);
        if (module == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Module Does Not Exist!)"); return; }
        module.enabled = bool.Parse(state);
        server.commands[server.commands.FindIndex(m => m.command_name == commandname)] = module;
        await server.UpdateOne();
        await ContextResponse.RespondAsync(context.Response, "[Success] (Module Updated)");
    }
    catch (Exception e) { Console.WriteLine($"{e.Message}\n{e.InnerException}\n\n{e.StackTrace}"); await ContextResponse.RespondAsync(context.Response, "[Failure] (An Error Occured)"); return; }
});
app.MapPost("/loadServerConfig", async (HttpContext context, string id) =>
{
    try
    {
        ulong idd = ulong.Parse(id);
        var server = await GuildServer.Get(idd);
        if (server == null) { await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
        await FileServerMiddleware.ReplyFile(context, "Content/Pages/serverconfig.html");

    }
    catch (Exception e) { Console.WriteLine($"{e.Message}\n{e.InnerException}\n\n{e.StackTrace}"); await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
});

app.MapGet("/createAccount", async (HttpContext context, string email, string password) =>
{
    if (email == "None" || password == "None") { await ContextResponse.RespondAsync(context.Response,"[Failure] (Invalid Input)"); return; }
    var getUser = await WebsiteSchema.Get(email);
    if (getUser != null) { await ContextResponse.RespondAsync(context.Response,"[Failure] (Already Registered)"); return; }
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
        _id = new Random().Next(int.MaxValue),
        IsBanned = false,
        lockDate = DateTime.MaxValue,
        lockRetries = 3,
        pairCode = DataEncryption.GetRandomString(5)[0],
        resetCode = null,
        URLThumbnail = string.Empty,
        permissionLevel = new List<DiscordServer>()
    };
    var res = await data.Upload();
    if (res)
    {
        await ContextResponse.RespondAsync(context.Response,"[Success] (Account Created)");
    }
    else
    {
        await ContextResponse.RespondAsync(context.Response,"[Failure] (Failed To Create Account!)");
    }
    return;
});
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
                await ContextResponse.RespondAsync(context.Response,"Verified");
            }
            else
            {
                var gu = await WebsiteSchema.Get(element.Item2);
                if (gu != null)
                {
                    gu.IsBanned = true;
                    await gu.Update();
                }
                await ContextResponse.RespondAsync(context.Response,"InvalidMove");
            }
        }
        else
        {
            await ContextResponse.RespondAsync(context.Response,"NotLoggedIn");
        }
    }
    else
    {
        await ContextResponse.RespondAsync(context.Response,"NotLoggedIn");
    }
    return;
});
#endregion

#region Profiles
app.MapGet("/userDetails", async (HttpContext context, string email, string id) =>
{
    try
    {
        var idd = int.Parse(id);
        var target_email = ServerStorage.Find(s => s.Item1 == idd);
        if (target_email == null || target_email.Item2 != email) { await ContextResponse.RespondAsync(context.Response,"[Failure] (Incorrect Email Recieved!)"); return; }
        var userDetails = await WebsiteSchema.Get(target_email.Item2);
        if (userDetails == null) { await ContextResponse.RespondAsync(context.Response,"[Failure] (Account Not Registered!)"); return; }
        await ContextResponse.RespondAsync(context.Response,"[Success] " +
            JsonSerializer.Serialize(new UserDetailsResponse() { pairCode = userDetails.pairCode, discordID = userDetails.DiscordId, profileURL = userDetails.URLThumbnail, servers = userDetails.permissionLevel, user = (await GuildUser.Get(userDetails.DiscordId ?? 0)) }));
        return;
    }
    catch (Exception) { await ContextResponse.RespondAsync(context.Response,"[Failure] (An Error Occured!)"); return; }
});
#endregion

app.MapDefaultControllerRoute();

app.MapRazorPages();

app.Run();

//just adds a respondasync option that prevents errors because of an incorrect return.
internal class ContextResponse
{
    public static async Task RespondAsync(HttpResponse response, string text, CancellationToken cancellationToken = default)
    {
        var obj = new Dictionary<string, string>(); obj.Add("header", "text/json"); obj.Add("response", text);
        await response.WriteAsync(JsonSerializer.Serialize(obj),Encoding.UTF8,cancellationToken);
    }
    public static async Task RespondAsync(HttpResponse response, string text,Encoding encoding, CancellationToken cancellationToken = default)
    {
        var obj = new Dictionary<string, string>(); obj.Add("header", "text/json"); obj.Add("response", text);
        await response.WriteAsync(JsonSerializer.Serialize(obj),encoding, cancellationToken);
    }
}
public class ServerOverviewList
{
    public DiscordServer server { get; set; } = new DiscordServer();
    public string Name { get; set; } = "Test Server";
}
public class UserDetailsResponse
{
    public string? pairCode { get; set; } = null;
    public ulong? discordID { get; set; } = null;
    public string profileURL { get; set; } = "Images/unknownprofliepic.png";
    public List<DiscordServer> servers { get; set; } = new List<DiscordServer>();
    public GuildUser? user { get; set; } = null;
}
public class UserResponse
{
    public string email { get; set; }
    public int sessionId { get; set; }
    public string URLThumbnail { get; set; }
    public int permissionLevel { get; set; }
}