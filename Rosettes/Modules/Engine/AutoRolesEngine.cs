﻿using Dapper;
using MySqlConnector;
using Rosettes.Core;

namespace Rosettes.Modules.Engine
{
    public static class AutoRolesEngine
    {
        private static List<AutoRoleEntry> AutoRolesEntries = new();
        private static List<AutoRoleGroup> AutoRolesGroups = new();

        public static ulong GetGuildIdFromMessage(ulong messageid)
        {
            try
            {
                return (AutoRolesGroups.First(x => x.MessageId == messageid).GuildId);
            }
            catch
            {
                return 0;
            }
        }

        public static IEnumerable<AutoRoleEntry> GetMessageRolesForEmote(ulong messageid, string emoteName)
        {
            IEnumerable<AutoRoleEntry> FoundEntries;
            uint parentGroup = (AutoRolesGroups.First(x => x.MessageId == messageid).Id);
            FoundEntries =
                from role in AutoRolesEntries
                where role.RoleGroupId == parentGroup && role.Emote == emoteName
                select role;
            return FoundEntries;
        }

        public static IEnumerable<AutoRoleEntry> GetMessageAutoroles(ulong messageid)
        {
            IEnumerable<AutoRoleEntry> FoundEntries;
            uint parentGroup = (AutoRolesGroups.First(x => x.MessageId == messageid).Id);
            FoundEntries =
                from role in AutoRolesEntries
                where role.RoleGroupId == parentGroup
                select role;
            return FoundEntries;
        }

        public static async Task<bool> SyncWithDatabase()
           
        {
            using var db = new MySqlConnection(Settings.Database.ConnectionString);

            var sql = @"SELECT guildid, emote, roleid, rolegroupid FROM autorole_entries";

            AutoRolesEntries = (await db.QueryAsync<AutoRoleEntry>(sql, new { })).ToList();

            sql = @"SELECT id, guildid, name, messageid FROM autorole_groups";

            AutoRolesGroups = (await db.QueryAsync<AutoRoleGroup>(sql, new { })).ToList();

            return true;
        }
    }

    public class AutoRoleEntry
    {
        public ulong GuildId;
        public string Emote;
        public ulong RoleId;
        public uint RoleGroupId;

        public AutoRoleEntry(ulong guildid, string emote, ulong roleid, uint rolegroupid)
        {
            GuildId = guildid;
            Emote = emote;
            RoleId = roleid;
            RoleGroupId = rolegroupid;
        }
    }

    public class AutoRoleGroup
    {
        public uint Id;
        public ulong GuildId;
        public ulong MessageId;
        public string Name;

        public AutoRoleGroup(uint id, ulong guildid, string name, ulong messageid)
        {
            Id = id;
            GuildId = guildid;
            Name = name;
            MessageId = messageid;
        }
    }
}