using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NareLauncher.Core
{
    public class Account
    {
        public string Name = "Player";
        public string Uuid = "";
        public string Type = "offline";      // offline / microsoft
        public string AccessToken = "0";
        public string RefreshToken = "";
        public DateTime ExpiresAt = DateTime.MinValue;
        /// <summary>Путь к PNG со скином, выбранным в редакторе.</summary>
        public string SkinPath = "";

        public bool IsOffline { get { return Type != "microsoft"; } }

        public string UserType { get { return IsOffline ? "legacy" : "msa"; } }

        public override string ToString()
        {
            return Name + (IsOffline ? "  ·  оффлайн" : "  ·  Microsoft");
        }
    }

    public static class AccountStore
    {
        public static List<Account> Accounts = new List<Account>();
        public static int SelectedIndex = -1;

        public static Account Selected
        {
            get
            {
                if (SelectedIndex >= 0 && SelectedIndex < Accounts.Count) return Accounts[SelectedIndex];
                return Accounts.Count > 0 ? Accounts[0] : null;
            }
        }

        public static void Load()
        {
            Accounts = new List<Account>();
            SelectedIndex = -1;

            var j = Json.ParseFile(Paths.AccountsFile);
            foreach (var a in j["accounts"].Items)
            {
                var acc = new Account
                {
                    Name = a["name"].AsString("Player"),
                    Uuid = a["uuid"].AsString(""),
                    Type = a["type"].AsString("offline"),
                    AccessToken = a["accessToken"].AsString("0"),
                    RefreshToken = a["refreshToken"].AsString(""),
                    SkinPath = a["skinPath"].AsString("")
                };
                DateTime exp;
                if (DateTime.TryParse(a["expiresAt"].AsString(""), out exp)) acc.ExpiresAt = exp;
                if (string.IsNullOrEmpty(acc.Uuid)) acc.Uuid = OfflineUuid(acc.Name);
                Accounts.Add(acc);
            }

            SelectedIndex = j["selected"].AsInt(-1);
            if (SelectedIndex >= Accounts.Count) SelectedIndex = Accounts.Count - 1;
        }

        public static void Save()
        {
            try
            {
                var root = Json.NewObject();
                var arr = Json.NewArray();
                foreach (var a in Accounts)
                {
                    var o = Json.NewObject();
                    o.Set("name", a.Name);
                    o.Set("uuid", a.Uuid);
                    o.Set("type", a.Type);
                    o.Set("accessToken", a.AccessToken);
                    o.Set("refreshToken", a.RefreshToken);
                    o.Set("expiresAt", a.ExpiresAt.ToString("o"));
                    o.Set("skinPath", a.SkinPath);
                    arr.Add(o);
                }
                root["accounts"] = arr;
                root.Set("selected", SelectedIndex);
                root.SaveTo(Paths.AccountsFile);
            }
            catch (Exception ex) { Log.Error("Не удалось сохранить аккаунты", ex); }
        }

        public static Account AddOffline(string nick)
        {
            if (string.IsNullOrWhiteSpace(nick)) nick = "Player";
            nick = nick.Trim();

            var acc = new Account
            {
                Name = nick,
                Type = "offline",
                Uuid = OfflineUuid(nick),
                AccessToken = "0"
            };
            Accounts.Add(acc);
            SelectedIndex = Accounts.Count - 1;
            Save();
            return acc;
        }

        /// <summary>
        /// Переименование. У оффлайн-аккаунта UUID считается от ника,
        /// поэтому он пересчитывается — как и в самой игре.
        /// </summary>
        public static bool Rename(int index, string nick)
        {
            if (index < 0 || index >= Accounts.Count) return false;
            if (string.IsNullOrWhiteSpace(nick)) return false;

            nick = nick.Trim();
            var acc = Accounts[index];
            acc.Name = nick;
            if (acc.IsOffline) acc.Uuid = OfflineUuid(nick);
            Save();
            return true;
        }

        public static bool NickTaken(string nick, int exceptIndex = -1)
        {
            for (int i = 0; i < Accounts.Count; i++)
            {
                if (i == exceptIndex) continue;
                if (string.Equals(Accounts[i].Name, nick, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static void Remove(int index)
        {
            if (index < 0 || index >= Accounts.Count) return;
            Accounts.RemoveAt(index);
            if (SelectedIndex >= Accounts.Count) SelectedIndex = Accounts.Count - 1;
            Save();
        }

        /// <summary>
        /// Тот же алгоритм, что и в самой игре для оффлайн-режима:
        /// UUID v3 от строки "OfflinePlayer:&lt;ник&gt;".
        /// </summary>
        public static string OfflineUuid(string nick)
        {
            byte[] data;
            using (var md5 = MD5.Create())
                data = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + nick));

            data[6] = (byte)((data[6] & 0x0F) | 0x30); // версия 3
            data[8] = (byte)((data[8] & 0x3F) | 0x80); // вариант IETF

            var sb = new StringBuilder(32);
            foreach (var b in data) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static string WithDashes(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) return uuid;
            uuid = uuid.Replace("-", "");
            if (uuid.Length != 32) return uuid;
            return uuid.Substring(0, 8) + "-" + uuid.Substring(8, 4) + "-" +
                   uuid.Substring(12, 4) + "-" + uuid.Substring(16, 4) + "-" + uuid.Substring(20);
        }

        public static bool IsValidNick(string nick)
        {
            if (string.IsNullOrWhiteSpace(nick)) return false;
            nick = nick.Trim();
            if (nick.Length < 3 || nick.Length > 16) return false;
            foreach (char c in nick)
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            return true;
        }
    }
}
