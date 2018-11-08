﻿using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace SharedLibraryCore.Database.Models
{
    public partial class EFClient
    {
        public enum ClientState
        {
            /// <summary>
            /// represents when the client has been detected as joining
            /// by the log file, but has not be authenticated by RCon
            /// </summary>
            Connecting,
            /// <summary>
            /// represents when the client has been authenticated by RCon
            /// and validated by the database
            /// </summary>
            Connected,
            /// <summary>
            /// represents when the client is leaving (either through RCon or log file)
            /// </summary>
            Disconnecting,
        }

        public enum Permission
        {
            /// <summary>
            /// client has been banned
            /// </summary>
            Banned = -1,
            /// <summary>
            /// default client state upon first connect
            /// </summary>
            User = 0,
            /// <summary>
            /// client has been flagged
            /// </summary>
            Flagged = 1,
            /// <summary>
            /// client is trusted
            /// </summary>
            Trusted = 2,
            /// <summary>
            /// client is a moderator
            /// </summary>
            Moderator = 3,
            /// <summary>
            /// client is an administrator
            /// </summary>
            Administrator = 4,
            /// <summary>
            /// client is a senior administrator
            /// </summary>
            SeniorAdmin = 5,
            /// <summary>
            /// client is a owner
            /// </summary>
            Owner = 6,
            /// <summary>
            /// not used
            /// </summary>
            Creator = 7,
            /// <summary>
            /// reserved for default account
            /// </summary>
            Console = 8
        }

        public EFClient()
        {
            ConnectionTime = DateTime.UtcNow;
            ClientNumber = -1;
            DelayedEvents = new Queue<GameEvent>();
            _additionalProperties = new Dictionary<string, object>
            {
                { "_reportCount", 0 }
            };
            CurrentAlias = CurrentAlias ?? new EFAlias();
        }

        public override string ToString()
        {
            return $"{Name}::{NetworkId}";
        }

        /// <summary>
        /// send a message directly to the connected client
        /// </summary>
        /// <param name="message">message content to send to client</param>
        public GameEvent Tell(String message)
        {
            var e = new GameEvent()
            {
                Message = message,
                Target = this,
                Owner = CurrentServer,
                Type = GameEvent.EventType.Tell,
                Data = message
            };

            this.CurrentServer?.Manager.GetEventHandler().AddEvent(e);
            return e;
        }

        /// <summary>
        /// warn a client with given reason
        /// </summary>
        /// <param name="warnReason">reason for warn</param>
        /// <param name="sender">client performing the warn</param>
        public GameEvent Warn(String warnReason, EFClient sender)
        {
            var e = new GameEvent()
            {
                Type = GameEvent.EventType.Warn,
                Message = warnReason,
                Data = warnReason,
                Origin = sender,
                Target = this,
                Owner = sender.CurrentServer
            };

            // enforce level restrictions
            if (this.Level > sender.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            else
            {
                this.Warnings++;
            }

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
            return e;
        }


        /// <summary>
        /// clear all warnings for a client
        /// </summary>
        /// <param name="sender">client performing the warn clear</param>
        /// <returns></returns>
        public GameEvent WarnClear(EFClient sender)
        {
            var e = new GameEvent()
            {
                Type = GameEvent.EventType.WarnClear,
                Origin = sender,
                Target = this,
                Owner = sender.CurrentServer
            };

            // enforce level restrictions
            if (sender.Level <= this.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
                return e;
            }

            this.Warnings = 0;

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
            return e;
        }

        /// <summary>
        /// report a client for a given reason
        /// </summary>
        /// <param name="reportReason">reason for the report</param>
        /// <param name="sender">client performing the report</param>
        /// <returns></returns>
        public GameEvent Report(string reportReason, EFClient sender)
        {
            var e = new GameEvent()
            {
                Type = GameEvent.EventType.Report,
                Message = reportReason,
                Data = reportReason,
                Origin = sender,
                Target = this,
                Owner = sender.CurrentServer
            };

            int reportCount = sender.GetAdditionalProperty<int>("_reportCount");

            if (this.Level > sender.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            else if (this.Equals(sender))
            {
                e.FailReason = GameEvent.EventFailReason.Invalid;
            }

            else if (reportCount > 2)
            {
                e.FailReason = GameEvent.EventFailReason.Throttle;
            }

            else if (CurrentServer.Reports.Count(report => (report.Origin.NetworkId == sender.NetworkId &&
                report.Target.NetworkId == this.NetworkId)) > 0)
            {
                e.FailReason = GameEvent.EventFailReason.Exception;
            }

            sender.SetAdditionalProperty("_reportCount", reportCount + 1);
            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
            return e;
        }

        /// <summary>
        /// flag a client for a given reason
        /// </summary>
        /// <param name="flagReason">reason for flagging</param>
        /// <param name="sender">client performing the flag</param>
        /// <returns>game event for the flag</returns>
        public GameEvent Flag(string flagReason, EFClient sender)
        {
            var e = new GameEvent()
            {
                Type = GameEvent.EventType.Flag,
                Origin = sender,
                Data = flagReason,
                Message = flagReason,
                Target = this,
                Owner = sender.CurrentServer
            };

            if (this.Level >= sender.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            else if (this.Level == Permission.Flagged)
            {
                e.FailReason = GameEvent.EventFailReason.Invalid;
            }

            else
            {
                this.Level = Permission.Flagged;
            }

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
            return e;
        }

        /// <summary>
        /// unflag a client for a given reason
        /// </summary>
        /// <param name="unflagReason">reason to unflag a player for</param>
        /// <param name="sender">client performing the unflag</param>
        /// <returns>game event for the un flug</returns>
        public GameEvent Unflag(string unflagReason, EFClient sender)
        {
            var e = new GameEvent()
            {
                Type = GameEvent.EventType.Unflag,
                Origin = sender,
                Target = this,
                Data = unflagReason,
                Message = unflagReason,
                Owner = sender.CurrentServer
            };

            if (sender.Level <= this.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            else if (this.Level != EFClient.Permission.Flagged)
            {
                e.FailReason = GameEvent.EventFailReason.Invalid;
            }

            else
            {
                this.Level = Permission.User;
            }

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
            return e;
        }

        /// <summary>
        /// kick a client for the given reason
        /// </summary>
        /// <param name="kickReason">reason to kick for</param>
        /// <param name="sender">client performing the kick</param>
        public GameEvent Kick(String kickReason, EFClient sender)
        {
            var e = new GameEvent()
            {
                Type = GameEvent.EventType.Kick,
                Message = kickReason,
                Target = this,
                Origin = sender,
                Data = kickReason,
                Owner = sender.CurrentServer
            };

            // enforce level restrictions
            if (this.Level > sender.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
            return e;
        }

        /// <summary>
        /// temporarily ban a client for the given time span
        /// </summary>
        /// <param name="tempbanReason">reason for the temp ban</param>
        /// <param name="banLength">how long the temp ban lasts</param>
        /// <param name="sender">client performing the tempban</param>
        public GameEvent TempBan(String tempbanReason, TimeSpan banLength, EFClient sender)
        {
            var e = new GameEvent()
            {
                Type = GameEvent.EventType.TempBan,
                Message = tempbanReason,
                Data = tempbanReason,
                Origin = sender,
                Target = this,
                Extra = banLength,
                Owner = sender.CurrentServer
            };

            // enforce level restrictions
            if (sender.Level <= this.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
            return e;
        }

        /// <summary>
        /// permanently ban a client
        /// </summary>
        /// <param name="banReason">reason for the ban</param>
        /// <param name="sender">client performing the ban</param>
        public GameEvent Ban(String banReason, EFClient sender)
        {
            var e = new GameEvent()
            {
                Type = GameEvent.EventType.Ban,
                Message = banReason,
                Data = banReason,
                Origin = sender,
                Target = this,
                Owner = sender.CurrentServer
            };

            // enforce level restrictions
            if (sender.Level <= this.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
            return e;
        }

        /// <summary>
        /// unban a client
        /// </summary>
        /// <param name="unbanReason">reason for the unban</param>
        /// <param name="sender">client performing the unban</param>
        /// <returns></returns>
        public GameEvent Unban(String unbanReason, EFClient sender)
        {
            var e = new GameEvent()
            {
                Type = GameEvent.EventType.Unban,
                Message = unbanReason,
                Data = unbanReason,
                Origin = sender,
                Target = this,
                Owner = sender.CurrentServer
            };

            // enforce level restrictions
            if (this.Level > sender.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
            return e;
        }

        /// <summary>
        /// Handles any client related logic on connection
        /// </summary>
        public void OnConnect()
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
#if !DEBUG
            if (Name.Length < 3)
            {
                CurrentServer.Logger.WriteDebug($"Kicking {this} because their name is too short");
                Kick(loc["SERVER_KICK_MINNAME"], Utilities.IW4MAdminClient(CurrentServer));
                return;
            }

            if (Name == "Unknown Soldier" ||
                Name == "UnknownSoldier" ||
                Name == "CHEATER")
            {
                CurrentServer.Logger.WriteDebug($"Kicking {this} because their name is generic");
                Kick(loc["SERVER_KICK_GENERICNAME"], Utilities.IW4MAdminClient(CurrentServer));
                return;
            }

            if (Name.Where(c => char.IsControl(c)).Count() > 0)
            {
                CurrentServer.Logger.WriteDebug($"Kicking {this} because their name contains control characters");
                Kick(loc["SERVER_KICK_CONTROLCHARS"], Utilities.IW4MAdminClient(CurrentServer));
                return;
            }

            // reserved slots stuff
            if (CurrentServer.MaxClients - (CurrentServer.GetClientsAsList().Count(_client => !_client.IsPrivileged())) < CurrentServer.ServerConfig.ReservedSlotNumber &&
               !this.IsPrivileged())
            {
                CurrentServer.Logger.WriteDebug($"Kicking {this} their spot is reserved");
                Kick(loc["SERVER_KICK_SLOT_IS_RESERVED"], Utilities.IW4MAdminClient(CurrentServer));
                return;
            }

            LastConnection = DateTime.UtcNow;
            Connections += 1;

#endif
        }

        public async Task OnDisconnect()
        {
            State = ClientState.Disconnecting;
            TotalConnectionTime += ConnectionLength;
            LastConnection = DateTime.UtcNow;
            await CurrentServer.Manager.GetClientService().Update(this);
        }

        public async Task OnJoin(int ipAddress)
        {
            // todo: fix this up
            var existingAlias = AliasLink.Children
                        .FirstOrDefault(a => a.Name == Name && a.IPAddress == ipAddress);

            if (existingAlias == null)
            {
                CurrentServer.Logger.WriteDebug($"Client {this} has connected previously under a different ip/name");

                CurrentAlias = new EFAlias()
                {
                    IPAddress = ipAddress,
                    Name = Name
                };
            }

            await CurrentServer.Manager.GetClientService().Update(this);

            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            var activePenalties = await CurrentServer.Manager.GetPenaltyService().GetActivePenaltiesAsync(AliasLinkId, ipAddress);
            var currentBan = activePenalties.FirstOrDefault(p => p.Type == Penalty.PenaltyType.Ban || p.Type == Penalty.PenaltyType.TempBan);

            var currentAutoFlag = activePenalties.Where(p => p.Type == Penalty.PenaltyType.Flag && p.PunisherId == 1)
                .Where(p => p.Active)
                .OrderByDescending(p => p.When)
                .FirstOrDefault();

            // remove their auto flag status after a week
            if (Level == Permission.Flagged &&
                currentAutoFlag != null &&
                (DateTime.UtcNow - currentAutoFlag.When).TotalDays > 7)
            {
                Level = Permission.User;
            }

            if (currentBan != null)
            {
                CurrentServer.Logger.WriteInfo($"Banned client {this} trying to join...");
                var autoKickClient = Utilities.IW4MAdminClient(CurrentServer);

                // reban the "evading" guid
                if (Level != Permission.Banned &&
                    currentBan.Type == Penalty.PenaltyType.Ban)
                {
                    // hack: re apply the automated offense to the reban
                    if (currentBan.AutomatedOffense != null)
                    {
                        autoKickClient.AdministeredPenalties.Add(new EFPenalty()
                        {
                            AutomatedOffense = currentBan.AutomatedOffense
                        });
                    }
                    Ban($"{currentBan.Offense}", autoKickClient);
                }

                // the player is permanently banned
                else if (currentBan.Type == Penalty.PenaltyType.Ban)
                {
                    Kick($"{loc["SERVER_BAN_PREV"]} {currentBan.Offense} ({loc["SERVER_BAN_APPEAL"]} {CurrentServer.Website})", autoKickClient);
                }

                else
                {
                    Kick($"{loc["SERVER_TB_REMAIN"]} ({(currentBan.Expires.Value - DateTime.UtcNow).TimeSpanText()} {loc["WEBFRONT_PENALTY_TEMPLATE_REMAINING"]})", autoKickClient);
                }
            }
        }

        [NotMapped]
        Dictionary<string, object> _additionalProperties;

        public T GetAdditionalProperty<T>(string name)
        {
            return _additionalProperties.ContainsKey(name) ? (T)_additionalProperties[name] : default(T);
        }

        public void SetAdditionalProperty(string name, object value)
        {
            if (_additionalProperties.ContainsKey(name))
            {
                _additionalProperties[name] = value;
            }
            else
            {
                _additionalProperties.Add(name, value);
            }
        }

        [NotMapped]
        public int ClientNumber { get; set; }
        [NotMapped]
        public int Ping { get; set; }
        [NotMapped]
        public int Warnings { get; set; }
        [NotMapped]
        public DateTime ConnectionTime { get; set; }
        [NotMapped]
        public int ConnectionLength => (int)(DateTime.UtcNow - ConnectionTime).TotalSeconds;
        [NotMapped]
        public Server CurrentServer { get; set; }
        [NotMapped]
        public int Score { get; set; }
        [NotMapped]
        public bool IsBot { get; set; }
        //private int _ipaddress;
        //public override int IPAddress
        //{
        //    get => _ipaddress;
        //    set => _ipaddress = value;
        //}
        //private string _name;
        //public override string Name
        //{
        //    get => _name;
        //    set => _name = value;
        //}
        [NotMapped]
        public ClientState State { get; set; }
        [NotMapped]
        public Queue<GameEvent> DelayedEvents { get; set; }
        [NotMapped]
        // this is kinda dirty, but I need localizable level names
        public ClientPermission ClientPermission => new ClientPermission()
        {
            Level = Level,
            Name = Utilities.CurrentLocalization
                .LocalizationIndex[$"GLOBAL_PERMISSION_{Level.ToString().ToUpper()}"]
        };

        public override bool Equals(object obj)
        {
            return ((EFClient)obj).NetworkId == this.NetworkId;
        }

        public override int GetHashCode()
        {
            return (int)NetworkId;
        }
    }
}