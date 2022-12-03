using CSharpWebsite.Content.Database;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AnniUpdate.Database
{
    public class GuildServer
    {
        [BsonId]
        public long _id { get; set; } = new Random().NextInt64(long.MinValue, long.MaxValue);
        public ulong GuildId { get; set; } = 0;
        public string GuildName { get; set; } = "Non-Existant Guild";
        public List<Module> eventModules { get; set; } = new List<Module>();
        public List<CommandReference> commands { get; set; } = new List<CommandReference>();
        public List<ulong> BannedIDs { get; set; } = new List<ulong>();
        public List<Tuple<ulong, DateTime, Tuple<int, int, int, int, int, int>>> TempBans { get; set; } = new List<Tuple<ulong, DateTime, Tuple<int, int, int, int, int, int>>>();
        public double Inflation { get; set; } = 0;
        public List<ChatResponses> chatResponses { get; set; } = new List<ChatResponses>();
        public static async Task<List<GuildServer>> GetAll()
        {
            try
            {
                var db = Controller.database;
                var collection = db.GetCollection<GuildServer>("DiscordServers");
                return collection.Find(_ => true).ToList();
            }
            catch (Exception)
            {
                await Task.Delay(100);
                return await GetAll();
            }
        }
        public static async Task<GuildServer?> Get(ulong Id)
        {
            try
            {
                var db = Controller.database;
                var collection = db.GetCollection<GuildServer>("DiscordServers");
                return collection.Find(u => u.GuildId == Id).FirstOrDefault();
            }
            catch (Exception ex)
            {
                await Task.Delay(100);
                return await Get(Id);
            }
        }
        public async Task<bool> UploadOne()
        {
            try
            {
                var db = Controller.database;
                var collection = db.GetCollection<GuildServer>("DiscordServers");
                var ins = collection.InsertOneAsync(this, new MongoDB.Driver.InsertOneOptions() { Comment = "Adding Guild User" });
                return ins.IsCompletedSuccessfully;
            }
            catch (Exception)
            {
                await Task.Delay(100);
                return await UploadOne();
            }
        }
        public async Task<bool> DeleteOne()
        {
            try
            {
                var db = Controller.database;
                var collection = db.GetCollection<GuildServer>("DiscordServers");
                var ins = await collection.DeleteOneAsync(g => g.GuildId == GuildId);
                return ins.IsAcknowledged;
            }
            catch (Exception)
            {
                await Task.Delay(100);
                return await DeleteOne();
            }
        }
        public static async Task<bool> UpdateMany(List<GuildServer> data)
        {
            try
            {
                IMongoDatabase db = Controller.database;
                var collection = db.GetCollection<GuildServer>("DiscordServers");
                var results = new List<bool>();
                for (int i = 0; i < data.Count; i++)
                {
                    var result = await collection.ReplaceOneAsync(s => s.GuildId == data.ElementAt(i).GuildId, data.ElementAt(i));
                    results.Add(result.IsAcknowledged);
                }
                return results.Where(r => r == true).Count() == results.Count;
            }
            catch (Exception)
            {
                await Task.Delay(100);
                return await UpdateMany(data);
            }
        }
        public async Task<bool> UpdateOne()
        {
            try
            {
                IMongoDatabase db = Controller.database;
                var collection = db.GetCollection<GuildServer>("DiscordServers");
                var replaceInfo = await collection.ReplaceOneAsync(db => db.GuildId == GuildId, this);
                return replaceInfo.IsAcknowledged;
            }
            catch (Exception)
            {
                await Task.Delay(100);
                return await UpdateOne();
            }
        }
    }
    public class CommandReference
    {
        public string command_name { get; set; } = "help";
        public bool enabled { get; set; } = true;
        public bool canBeDisabled { get; set; } = true;
    }
    public class Module
    {
        public string Name { get; set; } = "Test";
        public bool enabled { get; set; } = true;
        public bool canBeDisabled { get; set; } = true;
    }
}
