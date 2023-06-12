using CSharpWebsite.Content.Database;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AnniUpdate.Database
{
    //
    // Summary:
    //     Specifies the severity of the log message.
    public enum LogSeverity
    {
        //
        // Summary:
        //     Logs that contain the most severe level of error. This type of error indicate
        //     that immediate attention may be required.
        Critical,
        //
        // Summary:
        //     Logs that highlight when the flow of execution is stopped due to a failure.
        Error,
        //
        // Summary:
        //     Logs that highlight an abnormal activity in the flow of execution.
        Warning,
        //
        // Summary:
        //     Logs that track the general flow of the application.
        Info,
        //
        // Summary:
        //     Logs that are used for interactive investigation during development.
        Verbose,
        //
        // Summary:
        //     Logs that contain the most detailed messages.
        Debug
    }
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
        public LogSeverity logLevel { get; set; } = LogSeverity.Info;
        public bool canPrintLog(LogSeverity severity)
        {
            return (ulong)severity <= (ulong)logLevel;
        }
        public string serverPrefix { get; set; } = "a!";
        public ulong? logChannel { get; set; } = null;
        public List<Tuple<string, ulong>> channels { get; set; } = new List<Tuple<string, ulong>>();
        public List<StockReference> stocks { get; set; } = new List<StockReference>();
        public List<ChatResponses> chatResponses { get; set; } = new List<ChatResponses>();
        public List<StoreItemReference> storeItems { get; set; } = new List<StoreItemReference>();
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
                Console.WriteLine($"{ex.Message}\n{ex.InnerException}\n\n{ex.StackTrace}");
                await Task.Delay(100);
                return await Get(Id);
            }
        }
        public static async Task<GuildServer?> Get(string Id)
        {
            try
            {
                var db = Controller.database;
                var collection = db.GetCollection<GuildServer>("DiscordServers");
                var col = await GetAll();
                foreach (var item in col)
                {
                    if (item.GuildId.ToString() == Id)
                        return item;
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}\n{ex.InnerException}\n\n{ex.StackTrace}");
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
    public class Stock
    {
        public string Name { get; set; } = "Example Stock";
        /// <summary>
        /// Rate is 1 for every $1000 invested
        /// </summary>
        public double rate { get; set; } = double.MinValue;
        public ulong founder { get; set; } = ulong.MinValue;
        public List<Tuple<ulong, double>> investors { get; set; } = new List<Tuple<ulong, double>>();
        public StockReference Reference()
        {
            var refr = new StockReference { Name = Name, rate = rate, founder = founder.ToString() };
            List<Tuple<string, double>> invRef = new List<Tuple<string, double>>();
            foreach (var a in investors)
            {
                invRef.Add(Tuple.Create(a.Item1.ToString(), a.Item2));
            }
            refr.investors = invRef;
            return refr;
        }
    }
    public class StockReference
    {
        public string Name { get; set; } = new Stock().Name;
        public double rate { get; set; } = double.MinValue;
        public string founder { get; set; } = ulong.MinValue.ToString();
        public List<Tuple<string, double>> investors { get; set; } = new List<Tuple<string, double>>();
        public Stock Parse()
        {
            var refr = new Stock { Name = Name, rate = rate, founder = ulong.Parse(founder) };
            List<Tuple<ulong, double>> invRef = new List<Tuple<ulong, double>>();
            foreach (var a in investors)
            {
                invRef.Add(Tuple.Create(ulong.Parse(a.Item1), a.Item2));
            }
            refr.investors = invRef;
            return refr;
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
