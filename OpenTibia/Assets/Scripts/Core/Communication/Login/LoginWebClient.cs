﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine.Events;

namespace OpenTibiaUnity.Core.Communication.Login
{
    internal enum LoginWebClientError
    {
        None = 0,
        Technical = 1,
        // 2 unknown
        Login = 3,
        // 4, 5 unknown
        Authentication = 6,
    }

    internal class LoginWebClient : Web.WebClient
    {
        public class LoginErrorEvent : UnityEvent<string> { };
        public class LoginSuccessEvent : UnityEvent<Session, Playdata> { };

        internal string AccountIdentifier { get; set; }
        internal string Password { get; set; }
        internal string Token { get; set; }

        internal LoginErrorEvent onTechnicalError = new LoginErrorEvent();
        internal LoginErrorEvent onLoginError = new LoginErrorEvent();
        internal LoginErrorEvent onTokenError = new LoginErrorEvent();
        internal LoginSuccessEvent onLoginSuccess = new LoginSuccessEvent();

        internal LoginWebClient(int clientVersion, int buildVersion) : base(clientVersion, buildVersion) { }

        protected static string GetAccountIdentifierField() {
            if (OpenTibiaUnity.GameManager.GetFeature(GameFeature.GameAccountEmailAddress))
                return "email";
            return "accountname";
        }

        internal void LoadDataAsync(string requestURI) {
            var requestData = new Dictionary<string, string>();
            requestData.Add(GetAccountIdentifierField(), AccountIdentifier);
            requestData.Add("password", Password);
            if (Token.Length != 0)
                requestData.Add("token", Token);

            Connect(requestURI, Web.RequestType.Login, requestData);
        }

        protected override void OnParsingSuccess(JObject @object) {
            LoginWebClientError errorCode = LoginWebClientError.None;
            string errorMessage = string.Empty;
            foreach (var property in @object.Properties()) {
                if (property.Name == "errorCode")
                    errorCode = (LoginWebClientError)SafeInt(property.Value, 0);
                else if (property.Name == "errorMessage")
                    errorMessage = (string)property.Value;
            }

            if (errorCode == LoginWebClientError.Technical) {
                onTechnicalError.Invoke(errorMessage);
                return;
            } else if (errorCode == LoginWebClientError.Login) {
                onLoginError.Invoke(errorMessage);
                return;
            } else if (errorCode == LoginWebClientError.Authentication) {
                onTokenError.Invoke(errorMessage);
                return;
            } else if (errorCode != LoginWebClientError.None) {
                InvokeTechnicalParsingError();
                return;
            }

            var sessionObject = @object["session"] as JObject;
            var playDataObject = @object["playdata"] as JObject;
            if (sessionObject == null || playDataObject == null) {
                InvokeTechnicalParsingError();
                return;
            }

            var worldsArray = playDataObject["worlds"] as JArray;
            var charactersArray = playDataObject["characters"] as JArray;
            if (sessionObject == null || playDataObject == null) {
                InvokeTechnicalParsingError();
                return;
            }

            JToken sessionKeyToken = null,
                lastLoginTimeToken = null,
                statusToken = null;
            if (!sessionObject.TryGetValue("sessionkey", out sessionKeyToken)
                || !sessionObject.TryGetValue("lastlogintime", out lastLoginTimeToken)
                || !sessionObject.TryGetValue("status", out statusToken)) {
                InvokeTechnicalParsingError();
                return;
            }

            Session session = new Session() {
                SessionKey = SafeString(sessionKeyToken),
                Status = SafeString(statusToken),
                LastLoginTime = SafeUint(lastLoginTimeToken),
                PremiumUntil = SafeUint(sessionObject.GetValue("premiumuntil")),
                IsPremium = SafeBool(sessionObject.GetValue("ispremium")),
            };

            if (ClientVersion >= 1148) {
                session.FpsTracking = (bool)sessionObject.GetValue("fpstracking");
                session.IsReturner = (bool)sessionObject.GetValue("isreturner");
                session.ReturnerNotification = (bool)sessionObject.GetValue("returnernotification");
                session.ShowRewardNews = (bool)sessionObject.GetValue("showrewardnews");
            }

            if (ClientVersion >= 1149 && BuildVersion >= 5921) {
                session.OptionTracking = (bool)sessionObject.GetValue("optiontracking");
            }

            var playdata = new Playdata();

            List<int> worldIds = new List<int>();
            foreach (var worldObject in worldsArray.Children<JObject>()) {
                JToken idToken = null,
                nameToken = null,
                previewStateToken = null;

                if (!worldObject.TryGetValue("id", out idToken)
                    || !worldObject.TryGetValue("name", out nameToken)
                    || !worldObject.TryGetValue("previewstate", out previewStateToken))
                    continue;

                int worldID = SafeInt(idToken);
                if (worldIds.Contains(worldID))
                    continue;

                string externalAddress = string.Empty,
                    externalAddressProtected = string.Empty,
                    externalAddressUnprotected = string.Empty;

                int externalPort = 0,
                    externalPortProtected = 0,
                    externalPortUnprotected = 0;

                bool antiCheatProtection = true;

                if (ClientVersion >= 1148) {
                    JToken externalAddressProtectedToken = null,
                        externalPortProtectedToken = null;

                    if (!worldObject.TryGetValue("externaladdressprotected", out externalAddressProtectedToken)
                        || !worldObject.TryGetValue("externalportprotected", out externalPortProtectedToken))
                        continue;

                    externalAddressProtected = SafeString(externalAddressProtectedToken);
                    externalPortProtected = SafeInt(externalPortProtectedToken);
                }

                if (ClientVersion >= 1149 && BuildVersion >= 5921) {
                    JToken externalAddressUnprotectedToken = null,
                        externalPortUnprotectedToken = null;

                    if (!worldObject.TryGetValue("externaladdressunprotected", out externalAddressUnprotectedToken)
                        || !worldObject.TryGetValue("externalportunprotected", out externalPortUnprotectedToken))
                        continue;


                    externalAddressUnprotected = SafeString(externalAddressUnprotectedToken);
                    externalPortUnprotected = SafeInt(externalPortUnprotectedToken);
                } else {
                    JToken externalAddressToken = null,
                        externalPortToken = null;

                    if (!worldObject.TryGetValue("externaladdress", out externalAddressToken)
                        || !worldObject.TryGetValue("externalport", out externalPortToken))
                        continue;

                    externalAddress = SafeString(externalAddressToken);
                    externalPort = SafeInt(externalPortToken);
                }

                if (ClientVersion >= 1148 && worldObject.TryGetValue("anticheatprotection", out JToken antiChearProtectionToken))
                    antiCheatProtection = SafeBool(antiChearProtectionToken);

                bool isTournamentActive = false;
                bool isTournamentWorld = false;
                bool isRestrictedStore = false;
                int currentTournamentPhase = -1;
                if (ClientVersion >= 1215) {
                    isTournamentActive = SafeBool(worldObject.GetValue("istournamentactive"));
                    isTournamentWorld = SafeBool(worldObject.GetValue("istournamentworld"));
                    isRestrictedStore = SafeBool(worldObject.GetValue("restrictedstore"));
                    currentTournamentPhase = SafeInt(worldObject.GetValue("currenttournamentphase"));
                }

                worldIds.Add(worldID);
                playdata.Worlds.Add(new Playdata.World() {
                    ID = worldID,
                    PreviewState = SafeInt(previewStateToken),
                    ExternalPort = externalPort,
                    ExternalPortProtected = externalPortProtected,
                    ExternalPortUnprotected = externalPortUnprotected,

                    Name = SafeString(nameToken),
                    ExternalAddress = externalAddress,
                    ExternalAddressProtected = externalAddressProtected,
                    ExternalAddressUnprotected = externalAddressUnprotected,
                    AntiCheatProtection = antiCheatProtection,

                    // 12.00
                    PvpType = SafeInt(worldObject.GetValue("pvptype"), -1),

                    // 12.15
                    IsTournamentActive = SafeBool(worldObject.GetValue("istournamentactive")),
                    IsTournamentWorld = SafeBool(worldObject.GetValue("istournamentworld")),
                    RestrictStore = SafeBool(worldObject.GetValue("restrictedstore")),
                    CurrentTournamentPhase = SafeInt(worldObject.GetValue("currenttournamentphase"), -1),
                });
            }

            foreach (var characterObject in charactersArray.Children<JObject>()) {
                JToken worldIDToken = null,
                    nameToken = null;

                if (!characterObject.TryGetValue("worldid", out worldIDToken)
                    || !characterObject.TryGetValue("name", out nameToken))
                    continue;

                int worldID = SafeInt(worldIDToken);
                if (!worldIds.Contains(worldID))
                    continue;

                var character = new Playdata.Character() {
                    WorldID = worldID,
                    Name = SafeString(nameToken),

                    // 11.00 (x?)
                    IsMale = SafeBool(characterObject.GetValue("ismale")),
                    Tutorial = SafeBool(characterObject.GetValue("tutorial")),

                    // 12.00
                    Level = SafeInt(characterObject.GetValue("level")),
                    OutfitID = SafeInt(characterObject.GetValue("outfitid")),
                    HeadColor = SafeInt(characterObject.GetValue("headcolor")),
                    TorsoColor = SafeInt(characterObject.GetValue("torsocolor")),
                    LegsColor = SafeInt(characterObject.GetValue("legscolor")),
                    DetailColor = SafeInt(characterObject.GetValue("detailcolor")),
                    AddonsFlags = SafeInt(characterObject.GetValue("addonsflags")),

                    Vocation = SafeString(characterObject.GetValue("vocation")),

                    IsHidden = SafeBool(characterObject.GetValue("ishidden")),

                    // 12.15
                    IsTournamentParticipant = SafeBool(characterObject.GetValue("istournamentparticipant")),
                    RemainingDailyTournamentPlaytime = SafeInt(characterObject.GetValue("remainingdailytournamentplaytime")),
                };

                playdata.Characters.Add(character);
            }

            onLoginSuccess.Invoke(session, playdata);
        }
        
        protected override void OnParsingFailed() {
            InvokeTechnicalParsingError();
        }

        private void InvokeTechnicalParsingError() {
            if (ClientVersion >= 1200)
                onTechnicalError.Invoke(TextResources.LOGIN_WEB_CLIENT_LOGIN_ERROR_TECHNICAL_V12);
            else
                onTechnicalError.Invoke(TextResources.LOGIN_WEB_CLIENT_LOGIN_ERROR_RESPONSE_PARSING);
        }
    }
}
