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
        public int PermissionLevel { get; set; } = 0;
        public int LoginAttempts { get; set; } = 0;
        public bool IsLocked { get; set; } = false;
        public bool IsPaired { get; set; } = false;
        public ulong? DiscordId { get; set; } = null;
        public static async Task<WebsiteSchema?> Get(string email)
        {
            try
            {
                IMongoDatabase db = Controller.database;
                IMongoCollection<WebsiteSchema> collection = db.GetCollection<WebsiteSchema>("websitedatas");
                WebsiteSchema? item = null;
                foreach (var entry in await GetAll())
                {
                    Console.WriteLine($"Email: {DecryptEmail(entry)}");
                    if (DecryptEmail(entry).ToLower() == email.ToLower()) { item = entry; break; }
                    continue;
                }
                return item;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await Task.Delay(100);
                return await Get(email);
            }
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
            try
            {
                var db = Controller.database;
                IMongoCollection<WebsiteSchema> collection = db.GetCollection<WebsiteSchema>("websitedatas");
                return collection.Find(_ => true).ToList();
            }
            catch (Exception)
            {
                await Task.Delay(100);
                return await GetAll();
            }
        }
        public async Task<bool> Upload()
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
                return await Upload();
            }
        }
        public async Task<bool> Remove()
        {
            try
            {
                IMongoCollection<WebsiteSchema> collection = Controller.database.GetCollection<WebsiteSchema>("websitedatas");
                return collection.DeleteOne(w => w.Email == Email).IsAcknowledged;
            }
            catch (Exception)
            {
                await Task.Delay(100);
                return await Remove();
            }
        }
        public async Task<bool> Update()
        {
            try
            {
                IMongoCollection<WebsiteSchema> collection = Controller.database.GetCollection<WebsiteSchema>("websitedatas");
                return collection.ReplaceOne(d => d.Email == Email, this).IsAcknowledged;
            }
            catch (Exception)
            {
                await Task.Delay(100);
                return await Remove();
            }
        }
        public async Task<List<bool>> UpdateMany(List<WebsiteSchema> dataList)
        {
            List<bool> bools = new List<bool>();
            foreach (var data in dataList)
            {
                if (data == null) continue;
                bools.Add(await data.Update());
            }
            return bools;
        }
        public async Task<List<bool>> UploadMany(List<WebsiteSchema> dataList)
        {
            List<bool> bools = new List<bool>();
            foreach (var data in dataList)
            {
                if (data == null) continue;
                bools.Add(await data.Upload());
            }
            return bools;
        }
        public async Task<List<bool>> RemoveMany(List<WebsiteSchema> dataList)
        {
            List<bool> bools = new List<bool>();
            foreach (var data in dataList)
            {
                if (data == null) continue;
                bools.Add(await data.Remove());
            }
            return bools;
        }
    }
}
