﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.ProfanityDeterment
{
    public class Plugin : IPlugin
    {
        public string Name => "ProfanityDeterment";

        public float Version => Assembly.GetExecutingAssembly().GetName().Version.Major + Assembly.GetExecutingAssembly().GetName().Version.Minor / 10.0f;

        public string Author => "RaidMax";

        BaseConfigurationHandler<Configuration> Settings;

        public Task OnEventAsync(GameEvent E, Server S)
        {
            if (!Settings.Configuration().EnableProfanityDeterment)
                return Task.CompletedTask;

            if (E.Type == GameEvent.EventType.Connect)
            {
                E.Origin.SetAdditionalProperty("_profanityInfringements", 0);

                var objectionalWords = Settings.Configuration().OffensiveWords;
                bool containsObjectionalWord = objectionalWords.FirstOrDefault(w => E.Origin.Name.ToLower().Contains(w)) != null;
                var matchedFilters = new List<string>();

                // we want to run regex against it just incase
                if (!containsObjectionalWord)
                {
                    foreach (string word in objectionalWords)
                    {
                        if (Regex.IsMatch(E.Origin.Name.ToLower(), word, RegexOptions.IgnoreCase))
                        {
                            containsObjectionalWord |= true;
                            matchedFilters.Add(word);
                        }
                    }
                }

                if (containsObjectionalWord)
                {
                    var sender = Utilities.IW4MAdminClient(E.Owner);
                    sender.AdministeredPenalties = new List<EFPenalty>()
                    {
                        new EFPenalty()
                        {
                            AutomatedOffense = $"{E.Origin.Name} - {string.Join(",", matchedFilters)}"
                        }
                    };
                    E.Origin.Kick(Settings.Configuration().ProfanityKickMessage, sender);
                };
            }

            if (E.Type == GameEvent.EventType.Disconnect)
            {
                E.Origin.SetAdditionalProperty("_profanityInfringements", 0);
            }

            if (E.Type == GameEvent.EventType.Say)
            {
                var objectionalWords = Settings.Configuration().OffensiveWords;
                bool containsObjectionalWord = false;
                var matchedFilters = new List<string>();

                foreach (string word in objectionalWords)
                {
                    if (Regex.IsMatch(E.Data.ToLower(), word, RegexOptions.IgnoreCase))
                    {
                        containsObjectionalWord |= true;
                        matchedFilters.Add(word);
                    }
                }

                if (containsObjectionalWord)
                {
                    int profanityInfringments = E.Origin.GetAdditionalProperty<int>("_profanityInfringements");

                    var sender = Utilities.IW4MAdminClient(E.Owner);
                    sender.AdministeredPenalties = new List<EFPenalty>()
                    {
                        new EFPenalty()
                        {
                            AutomatedOffense = $"{E.Data} - {string.Join(",", matchedFilters)}"
                        }
                    };

                    if (profanityInfringments >= Settings.Configuration().KickAfterInfringementCount)
                    {
                        E.Origin.Kick(Settings.Configuration().ProfanityKickMessage, sender);
                    }

                    else if (profanityInfringments < Settings.Configuration().KickAfterInfringementCount)
                    {
                        E.Origin.SetAdditionalProperty("_profanityInfringements", profanityInfringments + 1);
                        E.Origin.Warn(Settings.Configuration().ProfanityWarningMessage, sender);
                    }
                }
            }
            return Task.CompletedTask;
        }

        public async Task OnLoadAsync(IManager manager)
        {
            // load custom configuration
            Settings = new BaseConfigurationHandler<Configuration>("ProfanityDetermentSettings");
            if (Settings.Configuration() == null)
            {
                Settings.Set((Configuration)new Configuration().Generate());
                await Settings.Save();
            }
        }

        public Task OnTickAsync(Server S) => Task.CompletedTask;

        public Task OnUnloadAsync() => Task.CompletedTask;
    }
}
