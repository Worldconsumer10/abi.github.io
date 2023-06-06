using AnniUpdate.Database;
using CSharpWebsite.Content.Database;
using MongoDB.Driver.Core.Operations;
using System;
using System.Text.Json;

namespace CSharpWebsite.Functions
{
    public class SessionUser
    {
        public string email { get; set; }
        public GuildUser? user { get; set; }
        public WebsiteSchema website { get; set; }
        public int sessionID { get; set; }
        public DateTime lastChecked { get; set; }
        public Task sessionTask { get; set; }
        public SessionUser(string email,WebsiteSchema schema, int sessionID, GuildUser? user=null)
        {
            this.email = email;
            this.user = user;
            this.website = schema;
            this.sessionID = sessionID;
            lastChecked = DateTime.Now;
            this.sessionTask = Task.Run(async () =>
            {
                while (true)
                {
                    if (DateTime.Now > lastChecked.AddHours(1))
                        break;
                    await Task.Delay(100);
                }
                if (sessionUsers.Any(s => s.email == email))
                    sessionUsers.RemoveAll(u => u.email == email);
            });
        }
        private static List<SessionUser> sessionUsers = new List<SessionUser>();
        private static double GenerateRandomDouble(double min, double max)
        {
            return new Random().NextDouble() * (max - min) + min;
        }
        public static string LoggedIn(WebsiteSchema user,string email)
        {
            int sessionIndex = new Random().Next(int.MinValue, int.MaxValue);
            var highestperm = 0;
            if (user.WebsiteOverride == null)
            {
                var highestpermlist = user.permissionLevel;
                highestpermlist.Sort((a, b) => { if (a.userLevel < b.userLevel) return 1; if (a.userLevel > b.userLevel) return -1; return 0; });
                if (highestpermlist.Count > 0)
                {
                    highestperm = highestpermlist[0].userLevel;
                }
            }
            else
            {
                highestperm = (int)user.WebsiteOverride;
            }
            if (!IsLoggedIn(email))
            {
                var nstorage = new SessionUser(email, user, sessionIndex, user.DiscordId != null ? GuildUser.Get((ulong)user.DiscordId).GetAwaiter().GetResult() : null);
                sessionUsers.Add(nstorage);
                return "[Success] (Logged In) " + JsonSerializer.Serialize(new UserResponse() { email = email, sessionId = nstorage.sessionID, URLThumbnail = user.URLThumbnail, permissionLevel = highestperm });
            }
            else
            {
                var storage = sessionUsers.Find(s => s.email.ToLower() == email.ToLower());
                var nstorage = new SessionUser(email, user, sessionIndex, null);
                if (storage != null)
                {
                    nstorage.sessionID = storage.sessionID;
                    nstorage.user = storage.user;
                }
                sessionUsers[sessionUsers.FindIndex(s => s.email.ToLower() == email.ToLower())] = nstorage;
                return "[Success] (Logged In) " + JsonSerializer.Serialize(new UserResponse() { email = email, sessionId = nstorage.sessionID, URLThumbnail = user.URLThumbnail, permissionLevel = highestperm });
            }
        }
        public static SessionUser? GetSessionUser(Func<SessionUser,bool> filter)
        {
            for (int i = 0; i < sessionUsers.Count; i++)
            {
                var us = sessionUsers[i];
                if (us != null && filter(us))
                    return us;
            }
            return null;
        }
        public static void RemoveAll(Func<SessionUser, bool> filter)
        {
            sessionUsers.RemoveAll(u=>filter(u));
        }
        public static bool? OnCheckin(int sessionID,string email)
        {
            var sess = sessionUsers.FirstOrDefault(u => u.email.ToLower() == email.ToLower());
            if (sess == null)
                return false;
            if (sess.sessionID != sessionID)
                return null;
            sess.lastChecked = DateTime.Now;
            sessionUsers[sessionUsers.FindIndex(u => u.sessionID == sessionID)] = sess;
            return true;
        }
        public static bool IsLoggedIn(string email)
        {
            return sessionUsers.Any(u => u.email.ToLower() == email.ToLower());
        }
    }
}
