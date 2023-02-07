using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace CSharpWebsite.Content.Database
{
    public class WebsiteSchema
    {
        [BsonId]
        public int _id { get; set; } = new Random().Next(int.MinValue, int.MaxValue);
        public List<string> Email { get; set; } = new List<string>();
        public List<string> Password { get; set; } = new List<string>();
        public List<string> EnryptionKey { get; set; } = new List<string>();
        public int LoginAttempts { get; set; } = 0;
        public string URLThumbnail { get; set; } = string.Empty;
        public bool IsLocked { get; set; } = false;
        public bool IsPaired { get; set; } = false;
        public int? WebsiteOverride { get; set; } = null;
        public bool IsBanned { get; set; } = false;
        public bool IsVerified { get; set; } = false;
        public DateTime creationDate { get; set; } = DateTime.Now;
        public ulong? DiscordId { get; set; } = null;
        public DateTime lockDate { get; set; } = DateTime.MaxValue;
        public int lockRetries { get; set; } = 3;
        public string? resetCode { get; set; } = null;
        public string? pairCode { get; set; } = DataEncryption.GetRandomString(5)[0];
        public List<DiscordServer> permissionLevel { get; set; } = new List<DiscordServer>();
        public static List<WebsiteSchema> websiteSchemas = new List<WebsiteSchema>();
        public static Task<WebsiteSchema?> Get(string email)
        {
            var task = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        IMongoDatabase db = Controller.database;
                        IMongoCollection<WebsiteSchema> collection = db.GetCollection<WebsiteSchema>("websitedatas");
                        WebsiteSchema? item = null;
                        foreach (var entry in await GetAll())
                        {
                            if (DecryptEmail(entry).ToLower() == email.ToLower()) { item = entry; break; }
                            continue;
                        }
                        return item;
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine($"{e.Message}\n{e.InnerException}\n\n{e.StackTrace}");
                        await Task.Delay(100);
                    }
                }
            });
            if (websiteSchemas.Any(u => DecryptEmail(u).ToLower() == email.ToLower())) return Task.FromResult(websiteSchemas.FirstOrDefault(u => DecryptEmail(u).ToLower() == email.ToLower())); else return task;
        }
        public static string DecryptEmail(WebsiteSchema data)
        {
            try
            {
                return DataEncryption.Decrypt(data.Email, data.EnryptionKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}\n{ex.InnerException}\n\n{ex.StackTrace}");
                return "An Error Occured";
            }
        }
        public static async Task<List<WebsiteSchema>> GetAll()
        {
            var task = Task.Run(async() =>
            {
                int loops = 0;
                while (true)
                {
                    try
                    {
                        var db = Controller.database;
                        IMongoCollection<WebsiteSchema> collection = db.GetCollection<WebsiteSchema>("websitedatas");
                        return collection.Find(_ => true).ToList();
                    }
                    catch (Exception)
                    {
                        if (loops > 10) return new List<WebsiteSchema>();
                        await Task.Delay(100);
                        loops++;
                    }
                }
            });
            websiteSchemas.AddRange((await task).Where(u => !websiteSchemas.Any(s => s._id == u._id)));
            return websiteSchemas;
        }
        public bool Upload()
        {
            websiteSchemas.Add(this);
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var db = Controller.database;
                        IMongoCollection<WebsiteSchema> collection = db.GetCollection<WebsiteSchema>("websitedatas");
                        var count = (await collection.FindAsync(_ => true)).ToList().Count;
                        await collection.InsertOneAsync(this);
                        return !(count > (await collection.FindAsync(_ => true)).ToList().Count);
                    }
                    catch (Exception)
                    {
                        await Task.Delay(100);
                    }
                }
            });
            return true;
        }
        public bool Remove()
        {
            websiteSchemas.RemoveAt(websiteSchemas.FindIndex(s => s._id == _id));
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        IMongoCollection<WebsiteSchema> collection = Controller.database.GetCollection<WebsiteSchema>("websitedatas");
                        return collection.DeleteOne(w => w.Email == Email).IsAcknowledged;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(100);
                    }
                }
            });
            return true;
        }
        public bool Update()
        {
            try { websiteSchemas[websiteSchemas.FindIndex(w => w._id == _id)] = this; return true; } catch { return false; }
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        IMongoCollection<WebsiteSchema> collection = Controller.database.GetCollection<WebsiteSchema>("websitedatas");
                        return collection.ReplaceOne(d => d.Email == Email, this).IsAcknowledged;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(100);
                    }
                }
            });

        }
        public async Task<List<bool>> UpdateMany(List<WebsiteSchema> dataList)
        {
            List<bool> bools = new List<bool>();
            foreach (var data in dataList)
            {
                if (data == null) continue;
                bools.Add(data.Update());
            }
            return bools;
        }
        public async Task<List<bool>> UploadMany(List<WebsiteSchema> dataList)
        {
            List<bool> bools = new List<bool>();
            foreach (var data in dataList)
            {
                if (data == null) continue;
                bools.Add(data.Upload());
            }
            return bools;
        }
        public async Task<List<bool>> RemoveMany(List<WebsiteSchema> dataList)
        {
            List<bool> bools = new List<bool>();
            foreach (var data in dataList)
            {
                if (data == null) continue;
                bools.Add(data.Remove());
            }
            return bools;
        }
    }
    public class ChatResponses
    {
        /// <summary>
        /// Phrases that this response looks for
        /// </summary>
        public List<string> phrases { get; set; } = new List<string>();
        /// <summary>
        /// response to these phrases
        /// </summary>
        public List<string> responses { get; set; } = new List<string>();
        /// <summary>
        /// Will this response be active?
        /// </summary>
        public bool enabled { get; set; }
    }
    public class DiscordServer
    {
        public ulong Id { get; set; } = 102412314;
        public int userLevel { get; set; } = 0;
        public string serverimgURL { get; set; } = string.Empty;
    }
}
