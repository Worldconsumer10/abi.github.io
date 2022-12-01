using CSharpWebsite.Content.Database;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AnniUpdate.Database
{
    public class GuildUser
    {
        [BsonId]
        public ulong ID { get; set; }
        public string Name { get; set; } = "Test User";
        public double Level { get; set; } = 1;
        public double XP { get; set; } = 0;
        public List<Earnings> Wallet { get; set; } = new List<Earnings>();
        public double Bank { get; set; } = 0;
        public bool rudeAnni { get; set; } = false;
        public double Bounty { get; set; } = 0;
        public GuildUser Clone()
        {
            return (GuildUser)MemberwiseClone();
        }
        public static async Task<List<GuildUser>> GetAll()
        {
            try
            {
                var db = Controller.database;
                var collection = db.GetCollection<GuildUser>("Users");
                return collection.Find(_ => true).ToList();
            }
            catch (Exception)
            {
                await Task.Delay(100);
                return await GetAll();
            }
        }
        public static async Task<GuildUser?> Get(ulong Id)
        {
            try
            {
                var db = Controller.database;
                var collection = db.GetCollection<GuildUser>("Users");
                return collection.Find(u => u.ID == Id).FirstOrDefault();
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
                var collection = db.GetCollection<GuildUser>("Users");
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
                var collection = db.GetCollection<GuildUser>("Users");
                var ins = await collection.DeleteOneAsync(g => g.ID == ID);
                return ins.IsAcknowledged;
            }
            catch (Exception)
            {
                await Task.Delay(100);
                return await DeleteOne();
            }
        }
        public static async Task<bool> UpdateMany(List<GuildUser> data)
        {
            try
            {
                IMongoDatabase db = Controller.database;
                IMongoCollection<GuildUser> collection = db.GetCollection<GuildUser>("Users");
                var results = new List<bool>();
                for (int i = 0; i < data.Count; i++)
                {
                    var result = await collection.ReplaceOneAsync(s => s.ID == data.ElementAt(i).ID, data.ElementAt(i));
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
                IMongoCollection<GuildUser> collection = db.GetCollection<GuildUser>("Users");
                var replaceInfo = await collection.ReplaceOneAsync(db => db.ID == ID, this);
                return replaceInfo.IsAcknowledged;
            }
            catch (Exception)
            {
                await Task.Delay(100);
                return await UpdateOne();
            }
        }
    }
    public class Earnings
    {
        public int ID { get; set; } = new Random().Next(int.MinValue, int.MaxValue);
        public double amount { get; set; } = 0;
        public DateTime earnDate { get; set; } = DateTime.Now;
        public bool canBeStolen { get; set; } = false;
        public string source { get; set; } = "Testing";
    }
}
