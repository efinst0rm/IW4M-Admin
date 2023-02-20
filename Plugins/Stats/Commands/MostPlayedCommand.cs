﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using SharedLibraryCore;
using System.Collections.Generic;
using Data.Abstractions;
using Data.Models.Client.Stats;
using SharedLibraryCore.Database.Models;
using IW4MAdmin.Plugins.Stats.Helpers;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    class MostPlayedCommand : Command
    {
        public static async Task<List<string>> GetMostPlayed(IGameServer gameServer, ITranslationLookup translationLookup,
            IDatabaseContextFactory contextFactory)
        {
            var serverId = gameServer.LegacyDatabaseId;

            var mostPlayed = new List<string>
            {
                $"(Color::Accent)--{translationLookup["PLUGINS_STATS_COMMANDS_MOSTPLAYED_TEXT"]}--"
            };

            await using var context = contextFactory.CreateContext(false);
            var thirtyDaysAgo = DateTime.UtcNow.AddMonths(-1);

            var iqStats = (from stats in context.Set<EFClientStatistics>()
                    join client in context.Clients
                        on stats.ClientId equals client.ClientId
                    join alias in context.Aliases
                        on client.CurrentAliasId equals alias.AliasId
                    where stats.ServerId == serverId
                    where client.Level != EFClient.Permission.Banned
                    where client.LastConnection >= thirtyDaysAgo
                    orderby stats.TimePlayed descending
                    select new
                    {
                        alias.Name,
                        stats.TimePlayed,
                        stats.Kills
                    })
                .Take(5);

            var iqList = await iqStats.ToListAsync();

            mostPlayed.AddRange(iqList.Select((stats, index) =>
                $"#{index + 1} " + translationLookup["COMMANDS_MOST_PLAYED_FORMAT_V2"].FormatExt(stats.Name, stats.Kills,
                    (DateTime.UtcNow - DateTime.UtcNow.AddSeconds(-stats.TimePlayed))
                    .HumanizeForCurrentCulture())));


            return mostPlayed;
        }

        private readonly IDatabaseContextFactory _contextFactory;

        public MostPlayedCommand(CommandConfiguration config, ITranslationLookup translationLookup,
            IDatabaseContextFactory contextFactory) : base(config, translationLookup)
        {
            Name = "mostplayed";
            Description = translationLookup["PLUGINS_STATS_COMMANDS_MOSTPLAYED_DESC"];
            Alias = "mp";
            Permission = EFClient.Permission.User;
            RequiresTarget = false;

            _contextFactory = contextFactory;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            var topStats = await GetMostPlayed(gameEvent.Owner, _translationLookup, _contextFactory);
            if (!gameEvent.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix))
            {
                await gameEvent.Origin.TellAsync(topStats, gameEvent.Owner.Manager.CancellationToken);
            }
            else
            {
                await gameEvent.Owner.BroadcastAsync(topStats);
            }
        }
    }
}
