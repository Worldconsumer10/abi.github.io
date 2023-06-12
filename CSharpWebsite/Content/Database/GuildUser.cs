using CSharpWebsite.Content.Database;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System.Collections.ObjectModel;

namespace AnniUpdate.Database
{
    public class Badge
    {
        public string Name { get; set; } = "Chatterbox";
        public string? NamePrefix { get; set; } = "The Chatterbox";
        public string AchievedBy { get; set; } = "Reaching Level 100";
        public int priority { get; set; } = 1;
        public Func<GuildUser, bool> condition { get; set; } = u => { return u.Level > 100; };
        public string? emojiBadge { get; set; }
        public bool badgeActive { get; set; } = false;
        public BadgeReference Reference(bool badgeActive = false)
        {
            return new BadgeReference { Name = Name, badgeActive = badgeActive };
        }
        public static List<BadgeReference> ReferenceList(List<Badge> badges)
        {
            List<BadgeReference> refs = new List<BadgeReference>();
            foreach (var bd in badges)
            {
                refs.Add(bd.Reference(bd.badgeActive));
            }
            return refs;
        }
    }
    public class BadgeReference
    {
        public string Name { get; set; } = "Chatterbox";
        public bool badgeActive { get; set; } = false;
    }
    public class DetLog
    {
        public string ID { get; private set; }
        public string CleanContent { get; private set; }
        public enum Action
        {
            Sent,
            Deleted,
            Modified,
            Pinned,
            Unpinned
        }
        public Tuple<string, string> executor { get; private set; }
        public Tuple<string, string> victim { get; private set; }
        public Action action { get; private set; }
        public string dateTime { get; private set; }
        public static DetLog Create(ulong id, string clean_content, Action action, Tuple<string, string> executor, Tuple<string, string> victim)
        {
            return new DetLog
            {
                ID = id.ToString(),
                CleanContent = clean_content,
                action = action,
                victim = victim,
                executor = executor,
                dateTime = DateTime.Now.Ticks.ToString()
            };
        }
    }
    public class TempAction
    {
        public ulong Id { get; set; }
        public enum Action
        {
            Mute,
            Ban,
            Blacklist
        }
        public Action type { get; set; }
        public DateTime start { get; set; }
        public TimeSpan duration { get; set; }
        public DateTime EndDate { get; set; }
        public TempActionReference Parse()
        {
            return new TempActionReference { durationTicks = duration.Ticks.ToString(), startTicks = start.Ticks.ToString(), type = type, EndDateTicks = EndDate.Ticks.ToString(), Id = Id.ToString() };
        }
    }
    public class TempActionReference
    {
        public string Id { get; set; }
        public TempAction.Action type { get; set; }
        public string startTicks { get; set; }
        public string durationTicks { get; set; }
        public string EndDateTicks { get; set; }
        public TempAction Parse()
        {
            return new TempAction { duration = new DateTime(long.Parse(durationTicks)).TimeOfDay, start = new DateTime(long.Parse(startTicks)), type = type, EndDate = new DateTime(long.Parse(EndDateTicks)), Id = ulong.Parse(Id) };
        }
    }
    public sealed class StoreItemReference
    {
        public string Name { get; set; } = "Test Item";
        public double Price { get; set; } = -1;
        public bool isEnabled { get; set; } = false; //can this appear in this guild store?
    }
    public class InventoryItem
    {
        public StoreItemReference reference { get; set; }
        public double count { get; set; }
    }
    public class DailyTaskReference
    {
        public string task { get; set; }
        public int count { get; set; }
    }
    public sealed class UserJobReference
    {
        public string Name { get; set; }
    }
    [BsonIgnoreExtraElements]
    public class GuildUser
    {
        [BsonId]
        public ulong ID { get; set; }
        [BsonRequired]
        public string Name { get; set; } = "Test User";
        public List<BadgeReference> badges { get; set; } = new List<BadgeReference>();
        [BsonRequired]
        public double Level { get; set; } = 1;
        [BsonRequired]
        public double XP { get; set; } = 0;
        [BsonElement("Wallet")]
        private List<Earnings> _wallet { get; set; } = new List<Earnings>();
        [BsonIgnore]
        public ReadOnlyCollection<Earnings> Wallet
        {
            get
            {
                return _wallet.AsReadOnly();
            }
        }
        [BsonRequired]
        public double Bank { get; set; } = 0;
        [BsonElement("Stamina")]
        private int _stamina = 100;
        [BsonIgnore]
        public int stamina
        {
            get
            {
                return _stamina;
            }
        }
        public string fakeBSB { get; set; } = CreateBSB();
        public int fakeAccNum { get; set; } = new Random().Next(0, int.MaxValue);
        public bool rudeAnni { get; set; } = false;
        public double Bounty { get; set; } = 0;
        public string? steamID { get; set; } = null;
        public List<DetLog> detLogs { get; set; } = new List<DetLog>();
        public List<TempActionReference> tempReferences { get; set; } = new List<TempActionReference>();
        public List<DailyTaskReference> dailyTasks { get; set; } = new List<DailyTaskReference>();
        [BsonElement("inventory")]
        private List<InventoryItem> _inventory { get; set; } = new List<InventoryItem>();
        public UserJobReference? jobReference { get; set; }
        [BsonIgnore]
        public ReadOnlyCollection<InventoryItem> inventory
        {
            get
            {
                return _inventory.AsReadOnly();
            }
        }
        public GuildUser(ulong iD, string name, double level, double xP, List<Earnings> wallet, double bank, string fakeBSB, int fakeAccNum, bool rudeAnni, double bounty)
        {
            ID = iD;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Level = level;
            XP = xP;
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Bank = bank;
            this.fakeBSB = fakeBSB ?? throw new ArgumentNullException(nameof(fakeBSB));
            this.fakeAccNum = fakeAccNum;
            this.rudeAnni = rudeAnni;
            Bounty = bounty;
        }

        public GuildUser Clone()
        {
            return (GuildUser)MemberwiseClone();
        }
        public static List<GuildUser> guildUsers = new List<GuildUser>();
        public static string CreateBSB()
        {
            var entries = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
                entries.Add($"{chars[new Random().Next(chars.Length)]}{chars[new Random().Next(chars.Length)]}");
            }
            return string.Join('-', entries);
        }
        public static async Task<List<GuildUser>> GetAll()
        {
            Task<List<GuildUser>> task = Task.Run(async () =>
            {
                int loop = 0;
                while (true)
                {
                    try
                    {
                        var db = Controller.database;
                        var collection = db.GetCollection<GuildUser>("Users");
                        return collection.Find(_ => true).ToList();
                    }
                    catch (Exception)
                    {
                        if (loop > 10) return new List<GuildUser>();
                        await Task.Delay(100);
                        loop++;
                    }
                }
            });
            guildUsers.AddRange((await task).Where(t => !guildUsers.Any(u => u.ID == t.ID)));
            return guildUsers;
        }
        public static Task<GuildUser?> Get(ulong Id)
        {
            var task = Task.Run(async () =>
            {
                int t = 0;
                while (true)
                {
                    try
                    {
                        var db = Controller.database;
                        var collection = db.GetCollection<GuildUser>("Users");
                        return collection.Find(u => u.ID == Id).FirstOrDefault();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"{e.Message}\n{e.InnerException}\n\n{e.StackTrace}");
                        if (t > 10) return null;
                        await Task.Delay(100);
                        t++;
                    }
                }
            });
            var user = guildUsers.FirstOrDefault(u => u.ID == Id);
            if (user!=null) return Task.FromResult(user); else return task;
        }
        public bool UploadOne()
        {
            guildUsers.Add(this);
            _ = Task.Run(async () =>
            {
                while (true)
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
                    }
                }
            });
            return true;
        }
        public bool DeleteOne()
        {
            guildUsers.RemoveAt(guildUsers.FindIndex(u => u.ID == ID));
            _ = Task.Run(async () =>
            {
                while (true)
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
                    }
                }
            });
            return true;
        }
        public static bool UpdateMany(List<GuildUser> data)
        {
            foreach (var sc in data)
            {
                try
                {
                    guildUsers[guildUsers.FindIndex(u => sc.ID == u.ID)] = sc;
                }
                catch { }
            }
            _ = Task.Run(async () =>
            {
                while (true)
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
                            try
                            {
                                guildUsers[guildUsers.FindIndex(u => u.ID == data.ElementAt(i).ID)] = data.ElementAt(i);
                            }
                            catch { }
                        }
                        return results.Where(r => r == true).Count() == results.Count;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(100);
                    }
                }
            });
            return true;
        }
        public async Task<bool> UpdateOne()
        {
            try
            {
                guildUsers[guildUsers.FindIndex(u => u.ID == ID)] = this;
            }
            catch { }
            _ = Task.Run(async () =>
            {
                while (true)
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
                    }
                }
            });
            return true;
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
