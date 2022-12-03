using AnniUpdate.Database;
using CSharpWebsite;
using CSharpWebsite.Content.Database;
using Microsoft.Extensions.FileProviders;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

HttpClient client = new HttpClient();

List<ResetRequest> resetRequests = new List<ResetRequest>();

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

app.UseExceptionHandler("/error");
app.UseHsts();

app.UseHttpsRedirection();
app.UseSession();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Environment.CurrentDirectory, "Content/ImageAssets")),
    RequestPath = "/Images"
});
app.UseAuthorization();

System.Timers.Timer verifyAccounts = new System.Timers.Timer(60000);
verifyAccounts.Elapsed += VerifyElapsed;
verifyAccounts.AutoReset = true;
verifyAccounts.Start();

async void VerifyElapsed(object? sender, System.Timers.ElapsedEventArgs e)
{
    var notverified = (await WebsiteSchema.GetAll()).Where(a => !a.IsVerified);
    foreach (var account in notverified)
    {
        if (account == null) continue;
        if (account.IsVerified) continue;
        if (account.creationDate.AddHours(24) > DateTime.Now)
        {
            TimeSpan timeleft = DateTime.Now.Subtract(account.creationDate.AddHours(24));
            if (timeleft.Hours == 12)
            {
                var decryptedEmail = DataEncryption.Decrypt(account.Email, account.EnryptionKey);
                var body = $"Hi {decryptedEmail}<br/>I have sent you this email in regards to your unverified account!<br/>Please click the link in the email sent when you created your account. This will verify your account, you have <{timeleft.Hours.ToString("D2")}:{timeleft.Minutes.ToString("D2")}:{timeleft.Seconds.ToString("D2")}>(HH:MM:SS) left before your account is terminated<br/><br/><i>This is an automated message! Do not reply!<i/>";
                SendEmail(decryptedEmail, "Account Verification", body);
            }
        }
        else
        {
            await account.Remove();
        }
    }
}

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
app.MapGet("/error", (HttpContext context) =>
{
});
app.MapPost("/error", (HttpContext context) =>
{
});
app.MapGet("/banned", async (HttpContext context) =>
{
    await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/error.html");
});
#endregion

#region AccountRelated
List<ServerStorage> ServerStorage = new List<ServerStorage>();
app.MapGet("/submitLogin", async (HttpContext context, string email, string password) =>
{
    if (email == "None" || password == "None") { await ContextResponse.RespondAsync(context.Response, "[Failure] (Invalid Input)"); return; }
    var getUser = await WebsiteSchema.Get(email);
    if (getUser == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Not Registered)"); return; }
    if (getUser.IsLocked) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Account Locked!)"); return; }
    if (getUser.IsBanned) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Account Banned!)"); return; }
    if (getUser.lockDate != DateTime.MaxValue) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Account Locked! Try Again Later!)"); return; }
    var decryptedEmail = DataEncryption.Decrypt(getUser.Email, getUser.EnryptionKey);
    var decryptPassword = DataEncryption.Decrypt(getUser.Password, getUser.EnryptionKey);
    if (decryptPassword != password)
    {
        if (getUser.lockRetries <= 0)
        {
            var keys = DataEncryption.GetRandomString(5);
            getUser.resetCode = keys[new Random().Next(keys.Count)];
            string ResetLink = $"{GetBaseUrl(context)}/emailverification?code={getUser.resetCode}";
            string Body = $"Dear {email}\nRecent activity on your account for website:<br/><br/>ubunifuserver.com<br/><br/>Has been marked as suspicious! Please follow this link:<br/>{ResetLink}<br/>Or use the code: {getUser.resetCode}<br/>When attempting to login next!<br/><br/><br/><i>If you did not try logging in recently it is suggested to keep an eye on your account as someone may be attempting to access it!</i><br/><br/>This is an automated message! Do not reply!";
            SendEmail(email, "Account Access Locked", Body);
            await ContextResponse.RespondAsync(context.Response, $"[Failure] (Incorrect Password. Resending Confirmation Email)");
        }
        else
        {
            getUser.lockRetries--;
            var keys = DataEncryption.GetRandomString(5);
            getUser.resetCode = keys[new Random().Next(keys.Count)];
            string ResetLink = $"{GetBaseUrl(context)}/emailverification?code={getUser.resetCode}";
            string Body = $"Dear {email}\nRecent activity on your account for website:<br/><br/>ubunifuserver.com<br/><br/>Has been marked as suspicious! Please follow this link:<br/>{ResetLink}<br/>Or use the code: {getUser.resetCode}<br/>When attempting to login next!<br/><br/><br/><i>If you did not try logging in recently it is suggested to keep an eye on your account as someone may be attempting to access it!</i><br/><br/>This is an automated message! Do not reply!";
            SendEmail(email, "Account Access Locked", Body);
            await ContextResponse.RespondAsync(context.Response, $"[Failure] (Incorrect Password. {getUser.lockRetries} Retries Left)");
        }
        await getUser.Update();
        return;
    }
    getUser.lockRetries = 3;
    await getUser.Update();
    var userIndex = new Random().Next(int.MinValue, int.MaxValue);
    var highestperm = 0;
    if (getUser.WebsiteOverride == null)
    {
        var highestpermlist = getUser.permissionLevel;
        highestpermlist.Sort((a, b) => { if (a.userLevel < b.userLevel) return 1; if (a.userLevel > b.userLevel) return -1; return 0; });
        if (highestpermlist.Count > 0)
        {
            highestperm = highestpermlist[0].userLevel;
        }
    }
    else
    {
        highestperm = (int)getUser.WebsiteOverride;
    }
    if (!ServerStorage.Any(s => s.email.ToLower() == decryptedEmail.ToLower()))
    {
        var nstorage = new ServerStorage()
        {
            email = decryptedEmail,
            id = userIndex,
            permissionLevel = highestperm
        };
        ServerStorage.Add(nstorage);
        Console.WriteLine($"New Session Started For: {decryptedEmail} (ID: {userIndex})");
        await ContextResponse.RespondAsync(context.Response, "[Success] (Logged In) " + JsonSerializer.Serialize(new UserResponse() { email = decryptedEmail, sessionId = nstorage.id, URLThumbnail = getUser.URLThumbnail, permissionLevel = highestperm }));
    }
    else
    {
        var storage = ServerStorage.Find(s => s.email.ToLower() == decryptedEmail.ToLower()) ?? new ServerStorage();
        var nstorage = new ServerStorage()
        {
            id = userIndex,
            email = decryptedEmail,
            permissionLevel = highestperm
        };
        ServerStorage[ServerStorage.FindIndex(s => s.email.ToLower() == decryptedEmail.ToLower())] = nstorage;
        Console.WriteLine($"Session Overwritten For: {decryptedEmail} (ID: {userIndex})");
        Console.WriteLine($"Session ID: {userIndex}");
        await ContextResponse.RespondAsync(context.Response, "[Success] (Logged In) " + JsonSerializer.Serialize(new UserResponse() { email = decryptedEmail, sessionId = nstorage.id, URLThumbnail = getUser.URLThumbnail, permissionLevel = highestperm }));
    }
    return;
});
app.MapGet("/emailverification", async (HttpContext context, string code) =>
{
    var user = (await WebsiteSchema.GetAll()).Find(c => c.resetCode == code);
    if (user == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] Invalid Code"); return; }
    user.lockDate = DateTime.MaxValue;
    user.lockRetries = 3;
    user.resetCode = null;
    await user.Update();
    await ContextResponse.RespondAsync(context.Response, "[Success] Account Unlocked!");
});
int requiredServer = 3;
app.MapPost("/sendConfigure", async (HttpContext context, string email, string id) =>
{
    try
    {
        var idi = LongParse(id);
        var storage = ServerStorage.Find(t => t.id == idi && t.email == email);
        if (storage == null) { await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
        var user = await WebsiteSchema.Get(storage.email);
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
        var idi = LongParse(id);
        var storage = ServerStorage.Find(t => t.id == idi && t.email == email);
        if (storage == null) { await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
        var user = await WebsiteSchema.Get(storage.email);
        if (user == null) { await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
        List<ServerOverviewList> lsits = new List<ServerOverviewList>();
        var serverlists = user.permissionLevel.Where(u => u.userLevel >= requiredServer);
        if (serverlists == null || serverlists.Count() <= 0) { await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
        foreach (var server in serverlists)
        {
            if (server == null) continue;
            lsits.Add(new ServerOverviewList()
            {
                server = server,
                Name = (await GuildServer.Get(server.Id))?.GuildName ?? "Unknown Guild"
            });
        }
        await ContextResponse.RespondAsync(context.Response, JsonSerializer.Serialize(lsits));
    }
    catch (Exception e)
    {
        Console.WriteLine($"{e.Message}\n{e.InnerException}\n\n{e.StackTrace}");
        await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return;
    }
});
app.MapGet("/getServerDetails", async (HttpContext context, string id) =>
{
    try
    {
        ulong idd = ULongParse(id);
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
        ulong idd = ULongParse(id);
        var server = await GuildServer.Get(idd);
        if (server == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Server Does Not Exist!)"); return; }
        var module = server.eventModules.Find(m => m.Name == moduleName);
        if (module == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Module Does Not Exist!)"); return; }
        if (!module.canBeDisabled) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Cannot Be Disabled)"); return; }
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
        ulong idd = ULongParse(id);
        var server = await GuildServer.Get(idd);
        if (server == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Server Does Not Exist!)"); return; }
        var module = server.commands.Find(m => m.command_name == commandname);
        if (module == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Module Does Not Exist!)"); return; }
        if (!module.canBeDisabled) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Cannot Be Disabled)"); return; }
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
        ulong idd = ULongParse(id);
        var server = await GuildServer.Get(idd);
        if (server == null) { await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
        await FileServerMiddleware.ReplyFile(context, "Content/Pages/serverconfig.html");

    }
    catch (Exception e) { Console.WriteLine($"{e.Message}\n{e.InnerException}\n\n{e.StackTrace}"); await FileServerMiddleware.ReplyFile(context, "Content/Pages/errorpages/403.html"); return; }
});

app.MapGet("/createAccount", async (HttpContext context, string email, string password) =>
{
    if (email == "None" || password == "None") { await ContextResponse.RespondAsync(context.Response, "[Failure] (Invalid Input)"); return; }
    var getUser = await WebsiteSchema.Get(email);
    if (getUser != null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Already Registered)"); return; }
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
        _id = new Random().Next(int.MinValue, int.MaxValue),
        IsBanned = false,
        lockDate = DateTime.MaxValue,
        lockRetries = 3,
        pairCode = DataEncryption.GetRandomString(5)[0],
        resetCode = null,
        URLThumbnail = string.Empty,
        permissionLevel = new List<DiscordServer>(),
        IsVerified = false,
        creationDate = DateTime.Now
    };
    var res = await data.Upload();
    if (res)
    {
        var verifylink = $"{GetBaseUrl(context)}/verifyAccount?id={data._id}";
        var body = $"Hi {email}!<br/>I see you want to create an account with me!<br/>Lets make this quick and easy!<br/>Just click this link:<br/>{verifylink}<br/><br/><i>This is an automated message! Do not reply!<i/>";
        SendEmail(email, "Verify Email", body);
        await ContextResponse.RespondAsync(context.Response, "[Success] (Account Created. Check Email)");
    }
    else
    {
        await ContextResponse.RespondAsync(context.Response, "[Failure] (Failed To Create Account!)");
    }
    return;
});
app.MapGet("/verifyAccount", async (HttpContext context, string id) =>
{
    var account = (await WebsiteSchema.GetAll()).Find(r => r._id.ToString() == id);
    if (account == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Not Registered!)"); return; }
    account.IsVerified = true;
    await account.Update();
    await FileServerMiddleware.ReplyFile(context, "Context/Pages/accountverified.html");
});
app.MapGet("/verifyLogin", async (HttpContext context, string json) =>
{
    var jsonResult = JsonSerializer.Deserialize<UserResponse>(json);
    if (jsonResult != null)
    {
        var element = ServerStorage.Find(s => s.id == jsonResult.sessionId);
        if (element != null && jsonResult != null)
        {
            if (element.email.ToLower() == jsonResult.email.ToLower())
            {
                await ContextResponse.RespondAsync(context.Response, "Verified");
            }
            else
            {
                var gu = await WebsiteSchema.Get(element.email);
                if (gu != null)
                {
                    gu.IsBanned = true;
                    await gu.Update();
                }
                await ContextResponse.RespondAsync(context.Response, "InvalidMove");
            }
        }
        else
        {
            await ContextResponse.RespondAsync(context.Response, "NotLoggedIn");
        }
    }
    else
    {
        await ContextResponse.RespondAsync(context.Response, "NotLoggedIn");
    }
    return;
});
#endregion

#region Profiles
app.MapGet("/userDetails", async (HttpContext context, string email, string id) =>
{
    try
    {
        var target_email = ServerStorage.Find(s => s.id.ToString() == id);
        if (target_email == null || target_email.email != email) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Incorrect Email Recieved!)"); return; }
        var userDetails = await WebsiteSchema.Get(target_email.email);
        if (userDetails == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Account Not Registered!)"); return; }
        var highestperm = 0;
        var highestpermlist = userDetails.permissionLevel;
        highestpermlist.Sort((a, b) => { if (a.userLevel < b.userLevel) return 1; if (a.userLevel > b.userLevel) return -1; return 0; });
        if (highestpermlist.Count > 0)
        {
            highestperm = highestpermlist[0].userLevel;
        }
        await ContextResponse.RespondAsync(context.Response, "[Success] " +
            JsonSerializer.Serialize(new UserDetailsResponse() { pairCode = userDetails.pairCode, discordID = userDetails.DiscordId, permissionLevel = highestperm, webPermLevel = userDetails.WebsiteOverride ?? 0, profileURL = userDetails.URLThumbnail, servers = userDetails.permissionLevel, user = (await GuildUser.Get(userDetails.DiscordId ?? 0)) }));
        return;
    }
    catch (Exception) { await ContextResponse.RespondAsync(context.Response, "[Failure] (An Error Occured!)"); return; }
});
#endregion

#region AccountReset
app.MapGet("/submitResetRequest", async (HttpContext context, string email) =>
{
    var femail = string.Join(string.Empty, email.Split('"'));
    resetRequests.RemoveAll(r => r.requestDate.AddMinutes(30) < DateTime.Now);
    var resetUser = await WebsiteSchema.Get(femail);
    if (resetUser == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Email Not Registered)"); return; }
    if (resetRequests.Any(r => r.email == femail))
    {
        resetRequests.RemoveAll(r => r.email == femail);
    }
    var resetRequest = new ResetRequest()
    {
        email = femail,
        requestDate = DateTime.Now,
        _id = new Random().NextInt64(long.MinValue, long.MaxValue)
    };
    resetRequests.Add(resetRequest);
    var resetUrl = $"{GetBaseUrl(context)}/resetPassword?id={resetRequest._id}";
    SendEmail(femail, "Account Password Reset", $"Hi {femail},<br/>I have recieved your request for a password reset!<br/>Lets get that underway shall we?<br/>I just need you to follow this link to reset your password<br/><br/>{resetUrl}<br/><br/><i>If you did not do this action, please disregard this email! The request will time out after 30 minutes<i/><br/>This is an automated message! Do not reply!");
    await ContextResponse.RespondAsync(context.Response, "[Success] (Reset Email Sent)");
});

app.MapGet("/resetPassword", async (HttpContext context, string id) =>
{
    var request = resetRequests.Find(r => r._id.ToString() == id);
    if (request == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Request Invalid)"); return; }
    if (request.requestDate.AddMinutes(30) < DateTime.Now) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Request Expired)"); return; }
    await FileServerMiddleware.ReplyFile(context, "Content/Pages/ResetPassword.html");
});
app.MapGet("/getRRequest", async (HttpContext context, string id) =>
{
    var request = resetRequests.Find(r => r._id.ToString() == id);
    if (request == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Request Invalid)"); return; }
    if (request.requestDate.AddMinutes(30) < DateTime.Now) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Request Expired)"); return; }
    await ContextResponse.RespondAsync(context.Response, "[Success] " + JsonSerializer.Serialize(request));
});
app.MapGet("/setNewPassword", async (HttpContext context, string id, string password) =>
{
    var request = resetRequests.Find(r => r._id.ToString() == id);
    if (request == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Request Invalid)"); return; }
    if (request.requestDate.AddMinutes(30) < DateTime.Now) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Request Expired)"); return; }
    var user = await WebsiteSchema.Get(request.email);
    if (user == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Not Registered)"); return; }
    var decryptedPassword = DataEncryption.Decrypt(user.Password, user.EnryptionKey);
    if (decryptedPassword.ToLower() == password.ToLower()) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Same Password)"); return; }
    if (GetPasswordSimilarity(decryptedPassword, password) > 50) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Passwords Too Similar!)"); return; }
    var encryptedPassword = DataEncryption.Encrypt(password, user.EnryptionKey);
    user.Password = encryptedPassword;
    var result = await user.Update();
    if (result)
    {
        await ContextResponse.RespondAsync(context.Response, "[Success] " + GetBaseUrl(context));
    }
    else
    {
        await ContextResponse.RespondAsync(context.Response, "[Failure] (Did Not Update)");
    }
});
#endregion

#region UserControl
List<ResetRequest> closeRequest = new List<ResetRequest>();
app.MapPost("/editUser", async (HttpContext context, string id) =>
{
    try
    {
        var storage = ServerStorage.Find(s => s.id.ToString() == id);
        if (storage == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Not Logged In!)"); return; }
        await FileServerMiddleware.ReplyFile(context, "Content/Pages/useredit.html");
    }
    catch (Exception e)
    {
        await ContextResponse.RespondAsync(context.Response, $"[Failure] ({e.Message}\n{e.Source}\n\n{e.StackTrace})"); return;
    }
});
app.MapPost("/closeAccount", async (HttpContext context, string id) =>
{
    var storage = ServerStorage.Find(s => s.id.ToString() == id);
    if (storage == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Not Logged In!)"); return; }
    ServerStorage.RemoveAll(s => s.id.ToString() == id);
    var request = new ResetRequest()
    {
        _id = new Random().NextInt64(long.MinValue, long.MaxValue),
        email = storage.email,
        requestDate = DateTime.Now
    };
    closeRequest.Add(request);
    string ResetLink = $"{GetBaseUrl(context)}/emailclose?code={request._id}";
    string Body = $"Dear {storage.email}<br/>I have recieved a request for your account to be terminated!<br/>Fear not! Closing your account can be done by just following this link:<br/>{ResetLink}<br/><i>If you did not do this action, please reset your password immediately!<i/><br/><br/>This is an automated message! Do not reply!";
    SendEmail(storage.email, "Account Close Request", Body);
    await FileServerMiddleware.ReplyFile(context, "Content/Pages/index.html");
});
app.MapGet("/emailclose", async (HttpContext context, string id) =>
{
    var request = resetRequests.Find(r => r._id.ToString() == id);
    if (request == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Request Invalid)"); return; }
    if (request.requestDate.AddMinutes(30) < DateTime.Now) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Request Expired)"); return; }
    var user = await WebsiteSchema.Get(request.email);
    if (user == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Not Registered)"); return; }
    await user.Remove();
    await ContextResponse.RespondAsync(context.Response, "[Success] (Account Closed!)");
});
app.MapPost("/lockAccount", async (HttpContext context, string id) =>
{
    var storage = ServerStorage.Find(s => s.id.ToString() == id);
    if (storage == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Not Logged In!)"); return; }
    ServerStorage.RemoveAll(s => s.id.ToString() == id);
    var user = await WebsiteSchema.Get(storage.email);
    if (user == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Not Registered!)"); return; }
    user.lockDate = DateTime.Now;
    user.lockRetries = 0;
    user.IsLocked = true;
    var keys = DataEncryption.GetRandomString(5);
    user.resetCode = keys[new Random().Next(keys.Count)];
    await user.Update();
    string ResetLink = $"{GetBaseUrl(context)}/emailverification?code={user.resetCode}";
    string Body = $"Dear {storage.email}<br/>As per your request your account has been locked! If you wish to unlock your account follow this link to restore it:<br/>{ResetLink}<br/>Or use the code: {user.resetCode}<br/>When attempting to login next!<br/><br/><br/>This is an automated message! Do not reply!";
    SendEmail(storage.email, "Account Locked", Body);
    await FileServerMiddleware.ReplyFile(context, "Content/Pages/index.html");
});
app.MapPost("/resetPasswordR", async (HttpContext context, string id) =>
{
    resetRequests.RemoveAll(r => r.requestDate.AddMinutes(30) < DateTime.Now);
    var storage = ServerStorage.Find(s => s.id.ToString() == id);
    if (storage == null) { await ContextResponse.RespondAsync(context.Response, "[Failure] (Not Logged In!)"); return; }
    ServerStorage.RemoveAll(s => s.id.ToString() == id);
    if (resetRequests.Any(r => r.email == storage.email))
    {
        resetRequests.RemoveAll(r => r.email == storage.email);
    }
    var resetRequest = new ResetRequest()
    {
        email = storage.email,
        requestDate = DateTime.Now,
        _id = new Random().NextInt64(long.MinValue, long.MaxValue)
    };
    resetRequests.Add(resetRequest);
    var resetUrl = $"{GetBaseUrl(context)}/resetPassword?id={resetRequest._id}";
    SendEmail(storage.email, "Account Password Reset", $"Hi {storage.email},<br/>I have recieved your request for a password reset!<br/>Lets get that underway shall we?<br/>I just need you to follow this link to reset your password<br/><br/>{resetUrl}<br/><br/><i>If you did not do this action, please disregard this email! The request will time out after 30 minutes<i/><br/>This is an automated message! Do not reply!");
    await FileServerMiddleware.ReplyFile(context, "Content/Pages/index.html");
});
#endregion

//returns a (%) of how similar the passwords are
int GetPasswordSimilarity(string a, string b)
{
    var largeststring = a.Length > b.Length ? a : b;
    int similarity = 0;
    for (int i = 0; i < largeststring.Length; i++)
    {
        if (i > b.Length || i > a.Length) break;
        var character1 = a[i];
        var character2 = b[i];
        if (char.IsWhiteSpace(character1) || char.IsWhiteSpace(character2)) continue;
        if (character1.ToString().ToLower() == character2.ToString().ToLower()) { similarity++; }
    }
    return (int)Math.Floor(((double)similarity / (double)largeststring.Length) * 100);
}

app.MapDefaultControllerRoute();

app.MapRazorPages();

app.Run();

long LongParse(string s)
{
    long nl = 0;
    ulong nul = 0;
    var res = ulong.TryParse(s, out nul);
    if (res) return long.MaxValue;
    long.TryParse(s, out nl);
    return nl;
}
ulong ULongParse(string s)
{
    ulong nul = 0;
    var res = ulong.TryParse(s, out nul);
    if (res) return nul;
    return ulong.MaxValue;
}

string GetBaseUrl(HttpContext context, bool includePort = true)
{
    var host = context.Request.Host.Host;
    var port = context.Request.Host.Port;
    var scheme = context.Request.Scheme;
    return $"{scheme}://{host}{(includePort ? $":{port}" : "")}";
}

void SendEmail(string email, string subject, string body)
{
    string SMTPServer = "smtp.gmail.com";
    int SMTP_Port = 587;
    string From = File.ReadLines(Path.Combine(Directory.GetCurrentDirectory(), "config.txt")).ElementAt(0);
    string Password = File.ReadLines(Path.Combine(Directory.GetCurrentDirectory(), "config.txt")).ElementAt(1);
    Console.WriteLine(File.ReadLines(Path.Combine(Directory.GetCurrentDirectory(), "config.txt")).ElementAt(1));
    if (Password == "nocode") return;
    string To = email;
    var smtpClient = new SmtpClient(SMTPServer, SMTP_Port)
    {
        DeliveryMethod = SmtpDeliveryMethod.Network,
        UseDefaultCredentials = false,
        EnableSsl = true
    };
    smtpClient.Credentials = new NetworkCredential(From, Password); //Use the new password, generated from google!
    var message = new MailMessage(new MailAddress(From, "Anni"), new System.Net.Mail.MailAddress(To, To));
    message.Subject = subject;
    message.IsBodyHtml = true;
    message.Body = body;
    smtpClient.Send(message);
}

internal class ServerStorage
{
    public int id { get; set; } = new Random().Next(int.MinValue, int.MaxValue);
    public string email { get; set; } = "fucking.failed@fuck.com";
    public int permissionLevel { get; set; } = 0;
}

//just adds a respondasync option that prevents errors because of an incorrect return.
internal class ContextResponse
{
    public static async Task RespondAsync(HttpResponse response, string text, CancellationToken cancellationToken = default)
    {
        var obj = new Dictionary<string, string>(); obj.Add("header", "text/json"); obj.Add("response", text);
        response.StatusCode = 200;
        await response.WriteAsync(JsonSerializer.Serialize(obj), Encoding.UTF8, cancellationToken);
    }
    public static async Task RespondAsync(HttpResponse response, string text, Encoding encoding, CancellationToken cancellationToken = default)
    {
        var obj = new Dictionary<string, string>(); obj.Add("header", "text/json"); obj.Add("response", text);
        response.StatusCode = 200;
        await response.WriteAsync(JsonSerializer.Serialize(obj), encoding, cancellationToken);
    }
}
public class ResetRequest
{
    public long _id { get; set; } = new Random().NextInt64(long.MinValue, long.MaxValue);
    public string email { get; set; } = string.Empty;
    public DateTime requestDate { get; set; } = DateTime.Now;
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
    public int webPermLevel { get; set; } = 0;
    public int permissionLevel { get; set; } = 0;
    public List<DiscordServer> servers { get; set; } = new List<DiscordServer>();
    public GuildUser? user { get; set; } = null;
}
public class UserResponse
{
    public string email { get; set; }
    public long sessionId { get; set; }
    public string URLThumbnail { get; set; }
    public int permissionLevel { get; set; }
}