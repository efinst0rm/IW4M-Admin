let vpnExceptionIds = [];
const vpnAllowListKey = 'Webfront::Nav::Admin::VPNAllowList';
const vpnWhitelistKey = 'Webfront::Profile::VPNWhitelist';

const init = (registerNotify, serviceResolver, config) => {
    registerNotify('IManagementEventSubscriptions.ClientStateAuthorized', (authorizedEvent, token) => plugin.onClientAuthorized(authorizedEvent, token));

    plugin.onLoad(serviceResolver, config);
    return plugin;
};

const plugin = {
    author: 'RaidMax',
    version: '1.6',
    name: 'VPN Detection Plugin',
    manager: null,
    config: null,
    logger: null,
    serviceResolver: null,
    translations: null,

    commands: [{
        name: 'whitelistvpn',
        description: 'whitelists a player\'s client id from VPN detection',
        alias: 'wv',
        permission: 'SeniorAdmin',
        targetRequired: true,
        arguments: [{
            name: 'player',
            required: true
        }],
        execute: (gameEvent) => {
            vpnExceptionIds.push(gameEvent.Target.ClientId);
            plugin.config.setValue('vpnExceptionIds', vpnExceptionIds);

            gameEvent.origin.tell(`Successfully whitelisted ${gameEvent.target.name}`);
        }
    },
        {
            name: 'disallowvpn',
            description: 'disallows a player from connecting with a VPN',
            alias: 'dv',
            permission: 'SeniorAdmin',
            targetRequired: true,
            arguments: [{
                name: 'player',
                required: true
            }],
            execute: (gameEvent) => {
                vpnExceptionIds = vpnExceptionIds.filter(exception => parseInt(exception) !== parseInt(gameEvent.Target.ClientId));
                plugin.config.setValue('vpnExceptionIds', vpnExceptionIds);

                gameEvent.origin.tell(`Successfully disallowed ${gameEvent.target.name} from connecting with VPN`);
            }
        }
    ],

    interactions: [{
        // registers the profile action
        name: vpnWhitelistKey,
        action: function(targetId, game, token) {
            const helpers = importNamespace('SharedLibraryCore.Helpers');
            const interactionData = new helpers.InteractionData();

            interactionData.actionPath = 'DynamicAction';
            interactionData.interactionId = vpnWhitelistKey;
            interactionData.entityId = targetId;
            interactionData.minimumPermission = 3;
            interactionData.source = plugin.name;
            interactionData.actionMeta.add('InteractionId', 'command'); // indicate we're wanting to execute a command
            interactionData.actionMeta.add('ShouldRefresh', true.toString()); // indicates that the page should refresh after performing the action

            if (vpnExceptionIds.includes(targetId)) {
                interactionData.name = plugin.translations['WEBFRONT_VPN_BUTTON_DISALLOW']; // text for the profile button
                interactionData.displayMeta = 'oi-circle-x';

                interactionData.actionMeta.add('Data', `disallowvpn`); // command to execute
                interactionData.actionMeta.add('ActionButtonLabel', plugin.translations['WEBFRONT_VPN_ACTION_DISALLOW_CONFIRM']); // confirm button on the dialog
                interactionData.actionMeta.add('Name', plugin.translations['WEBFRONT_VPN_ACTION_DISALLOW_TITLE']); // title on the confirm dialog
            } else {
                interactionData.name = plugin.translations['WEBFRONT_VPN_ACTION_ALLOW']; // text for the profile button
                interactionData.displayMeta = 'oi-circle-check';

                interactionData.actionMeta.add('Data', `whitelistvpn`); // command to execute
                interactionData.actionMeta.add('ActionButtonLabel', plugin.translations['WEBFRONT_VPN_ACTION_ALLOW_CONFIRM']); // confirm button on the dialog
                interactionData.actionMeta.add('Name', plugin.translations['WEBFRONT_VPN_ACTION_ALLOW_TITLE']); // title on the confirm dialog
            }

            return interactionData;
        }
    },
        {
            name: vpnAllowListKey,
            action: function(targetId, game, token) {
                const helpers = importNamespace('SharedLibraryCore.Helpers');
                const interactionData = new helpers.InteractionData();

                interactionData.name = plugin.translations['WEBFRONT_NAV_VPN_TITLE']; // navigation link name
                interactionData.description = plugin.translations['WEBFRONT_NAV_VPN_DESC']; // alt and title
                interactionData.displayMeta = 'oi-circle-check'; // nav icon
                interactionData.interactionId = vpnAllowListKey;
                interactionData.minimumPermission = 3; // moderator
                interactionData.interactionType = 2; // 1 is RawContent for apis etc..., 2 is 
                interactionData.source = plugin.name;

                interactionData.scriptAction = (sourceId, targetId, game, meta, token) => {
                    const clientsData = plugin.getClientsData(vpnExceptionIds);

                    let table = '<table class="table bg-dark-dm bg-light-lm">';

                    const disallowInteraction = {
                        InteractionId: 'command',
                        Data: 'disallowvpn',
                        ActionButtonLabel: plugin.translations['WEBFRONT_VPN_ACTION_DISALLOW_CONFIRM'],
                        Name: plugin.translations['WEBFRONT_VPN_ACTION_DISALLOW_TITLE']
                    };

                    if (clientsData.length === 0) {
                        table += `<tr><td>No players are whitelisted.</td></tr>`;
                    }

                    clientsData.forEach(client => {
                        table += `<tr>
                                    <td>
                                        <a href="/Client/Profile/${client.clientId}" class="level-color-${client.level.toLowerCase()} no-decoration">${client.currentAlias.name.stripColors()}</a>
                                    </td>
                                    <td>
                                        <a href="#" class="profile-action no-decoration float-right" data-action="DynamicAction" data-action-id="${client.clientId}"
                                           data-action-meta="${encodeURI(JSON.stringify(disallowInteraction))}">
                                            <div class="btn">
                                                <i class="oi oi-circle-x mr-5 font-size-12"></i>
                                                <span class="text-truncate">${plugin.translations['WEBFRONT_VPN_BUTTON_DISALLOW']}</span>
                                            </div>
                                        </a>
                                    </td>
                                </tr>`;
                    });

                    table += '</table>';

                    return table;
                };

                return interactionData;
            }
        }
    ],

    onClientAuthorized: function(authorizeEvent, token) {
        if (authorizeEvent.client.isBot) {
            return;
        }
        this.checkForVpn(authorizeEvent.client, token);
    },

    onLoad: function(serviceResolver, config) {
        this.serviceResolver = serviceResolver;
        this.config = config;
        this.manager = this.serviceResolver.resolveService('IManager');
        this.logger = this.serviceResolver.resolveService('ILogger', ['ScriptPluginV2']);
        this.translations = this.serviceResolver.resolveService('ITranslationLookup');

        this.config.setName(this.name); // use legacy key
        this.config.getValue('vpnExceptionIds').forEach(element => vpnExceptionIds.push(parseInt(element)));
        this.logger.logInformation(`Loaded ${vpnExceptionIds.length} ids into whitelist`);

        this.interactionRegistration = this.serviceResolver.resolveService('IInteractionRegistration');
        this.interactionRegistration.unregisterInteraction(vpnWhitelistKey);
        this.interactionRegistration.unregisterInteraction(vpnAllowListKey);
    },

    checkForVpn: function(origin, token) {
        let exempt = false;
        // prevent players that are exempt from being kicked
        vpnExceptionIds.forEach(function(id) {
            if (parseInt(id) === parseInt(origin.clientId)) {
                exempt = true;
                return false;
            }
        });

        if (exempt) {
            this.logger.logInformation(`{origin} is whitelisted, so we are not checking VPN status`, origin);
            return;
        }

        let usingVPN = false;

        const threading = importNamespace('System.Threading');
        const httpClient = new System.Net.Http.HttpClient();
        const tokenSource = new threading.CancellationTokenSource(250);
        const userAgent = `IW4MAdmin-${this.manager.getApplicationSettings().configuration().id}`;
        httpClient.defaultRequestHeaders.add('User-Agent', userAgent);

        try {
            const response = httpClient.getAsync(`https://api.xdefcon.com/proxy/check/?ip=${origin.IPAddressString}`, tokenSource.token).getAwaiter().getResult();
            const content = response.content;
            const parsedJSON = JSON.parse(content.readAsStringAsync(tokenSource.token).getAwaiter().getResult());
            response.dispose();
            content.dispose();
 
            usingVPN = parsedJSON.success && parsedJSON.proxy;
        } catch (ex) {
            tokenSource.dispose();
            httpClient.dispose();
            this.logger.logWarning('There was a problem checking client IP for VPN {message}', ex.message);
        }

        if (usingVPN) {
            this.logger.logInformation('{origin} is using a VPN ({ip})', origin.toString(), origin.ipAddressString);
            const contactUrl = this.manager.getApplicationSettings().configuration().contactUri;
            let additionalInfo = '';
            if (contactUrl) {
                additionalInfo = this.translations['SERVER_KICK_VPNS_NOTALLOWED_INFO'] + ' ' + contactUrl;
            }
            origin.kick(this.translations['SERVER_KICK_VPNS_NOTALLOWED'] + ' ' + additionalInfo, origin.currentServer.asConsoleClient());
        } else {
            this.logger.logDebug('{client} is not using a VPN', origin);
        }
    },

    getClientsData: function(clientIds) {
        const contextFactory = this.serviceResolver.resolveService('IDatabaseContextFactory');
        const context = contextFactory.createContext(false);
        const clientSet = context.clients;
        const clients = clientSet.getClientsBasicData(clientIds);
        context.dispose();

        return clients;
    }
};
