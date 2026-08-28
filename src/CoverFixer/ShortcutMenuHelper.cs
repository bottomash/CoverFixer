using System;
using System.IO;
using System.Text;
using MediaBrowser.Model.Logging;

namespace CoverFixer;

internal static class ShortcutMenuHelper
{
    private const string Injection = @"
const coverFixerHiddenDetailCommandIds = new Set([
    'favorite',
    'unfavorite',
    'markplayed',
    'markunplayed',
    'shuffle',
    'addtocollection',
    'addtoplaylist',
    'sync',
    'convert',
    'delete'
]);

function coverFixerIsManagedDetailsOptions(options) {
    return options.items?.length === 1 &&
        ['Series', 'Episode'].includes(options.items[0].Type) &&
        options.navigateOnDelete === 'back' &&
        options.positionY === 'center';
}

function coverFixerShowDeleteButton(options) {
    if (!coverFixerIsManagedDetailsOptions(options) || !options.items[0].CanDelete ||
        !(options.user && options.user.Policy.IsAdministrator)) {
        return;
    }

    setTimeout(() => {
        const activeView = Array.from(document.querySelectorAll('.itemView'))
            .find(view => view.offsetParent !== null);
        activeView?.querySelector('.btnDeleteItem')?.classList.remove('hide');
    }, 0);
}

const coverFixerCommandSource = {
    getCommands: function(options) {
        coverFixerShowDeleteButton(options);
        if (options.items?.length !== 1 || options.items[0].Type !== 'Series' ||
            !(options.user && options.user.Policy.IsAdministrator)) {
            return [];
        }

        const locale = this.globalize.getCurrentLocale().toLowerCase();
        const commandName = locale === 'zh-cn'
            ? '\u5237\u65b0 TMDB \u5c01\u9762'
            : (['zh-hk', 'zh-tw'].includes(locale) ? '\u66f4\u65b0 TMDB \u5c01\u9762' : 'Refresh TMDB Cover');
        return [{ name: commandName, id: 'coverfixer_refresh_tmdb_cover', icon: 'image_search' }];
    },

    executeCommand: function(command, items) {
        if (command !== 'coverfixer_refresh_tmdb_cover' || !items?.length) {
            return Promise.resolve();
        }

        return require(['connectionManager', 'globalize', 'loading', 'toast', 'confirm']).then(responses => {
            const connectionManager = responses[0];
            const globalize = responses[1];
            const loading = responses[2];
            const toast = responses[3];
            const confirm = responses[4];
            const locale = globalize.getCurrentLocale().toLowerCase();
            const title = locale === 'zh-cn'
                ? '\u5237\u65b0 TMDB \u5c01\u9762'
                : (['zh-hk', 'zh-tw'].includes(locale) ? '\u66f4\u65b0 TMDB \u5c01\u9762' : 'Refresh TMDB Cover');
            const message = locale === 'zh-cn'
                ? '\u5c06\u4f7f\u7528 TMDB \u8fdc\u7a0b\u5c01\u9762\u66ff\u6362\u5f53\u524d\u5c01\u9762\uff0c\u662f\u5426\u7ee7\u7eed\uff1f'
                : (['zh-hk', 'zh-tw'].includes(locale)
                    ? '\u5c07\u4f7f\u7528 TMDB \u9060\u7aef\u5c01\u9762\u53d6\u4ee3\u76ee\u524d\u5c01\u9762\uff0c\u662f\u5426\u7e7c\u7e8c\uff1f'
                    : 'Replace the current cover with a remote TMDB cover?');

            return confirm({
                text: message,
                title: title,
                confirmText: globalize.translate('Refresh'),
                primary: 'cancel'
            }).then(() => {
                loading.show();
                const apiClient = connectionManager.currentApiClient();
                const refreshUrl = apiClient.getUrl(`CoverFixer/Series/${items[0].Id}/Refresh`);
                return apiClient.ajax({
                    type: 'POST',
                    url: refreshUrl,
                    data: {},
                    contentType: 'application/json'
                }).then(() => {
                    toast(locale === 'zh-cn'
                        ? '\u5df2\u5237\u65b0 TMDB \u5c01\u9762'
                        : (['zh-hk', 'zh-tw'].includes(locale) ? '\u5df2\u66f4\u65b0 TMDB \u5c01\u9762' : 'TMDB cover refreshed'));
                    setTimeout(() => window.location.reload(), 400);
                }).catch(error => {
                    toast(locale === 'zh-cn'
                        ? '\u5237\u65b0 TMDB \u5c01\u9762\u5931\u8d25'
                        : (['zh-hk', 'zh-tw'].includes(locale) ? '\u66f4\u65b0 TMDB \u5c01\u9762\u5931\u6557' : 'Failed to refresh TMDB cover'));
                    throw error;
                }).finally(() => loading.hide());
            });
        });
    }
};

Emby.importModule('./modules/common/globalize.js').then(globalize => {
    coverFixerCommandSource.globalize = globalize;
    Emby.importModule('./modules/common/itemmanager/itemmanager.js').then(itemmanager => {
        if (!itemmanager.coverFixerDetailMenuPatched) {
            const originalGetCommands = itemmanager.getCommands.bind(itemmanager);
            itemmanager.getCommands = function(options) {
                const commands = originalGetCommands(options);
                if (!coverFixerIsManagedDetailsOptions(options) ||
                    !(options.user && options.user.Policy.IsAdministrator) ||
                    !options.positionTo?.classList.contains('btnMoreCommands')) {
                    return commands;
                }

                const filtered = commands.filter(command =>
                    !coverFixerHiddenDetailCommandIds.has(command.id));
                if (filtered.length) {
                    filtered[filtered.length - 1].dividerAfter = false;
                }
                return filtered;
            };
            itemmanager.coverFixerDetailMenuPatched = true;
        }
        itemmanager.registerCommandSource(coverFixerCommandSource);
    });
});
";

    public static byte[] ModifiedShortcutsBytes { get; private set; } = Array.Empty<byte>();

    private static string _shortcutsPath = string.Empty;

    public static void Initialize(string programSystemPath, ILogger logger)
    {
        try
        {
            _shortcutsPath = Path.Combine(
                programSystemPath,
                "dashboard-ui",
                "modules",
                "shortcuts.js");
            string originalShortcuts = File.ReadAllText(_shortcutsPath, Encoding.UTF8);
            ModifiedShortcutsBytes = Encoding.UTF8.GetBytes(BuildModifiedScript(originalShortcuts));
            logger.Info("已注入剧集详情菜单命令：刷新 TMDB 封面");
        }
        catch (Exception error)
        {
            ModifiedShortcutsBytes = Array.Empty<byte>();
            logger.ErrorException("注入剧集详情菜单命令失败", error);
        }
    }

    internal static string BuildModifiedScript(string originalShortcuts) =>
        originalShortcuts + Environment.NewLine + Injection;

    public static ReadOnlyMemory<byte> GetShortcuts()
    {
        if (ModifiedShortcutsBytes.Length > 0)
        {
            return ModifiedShortcutsBytes;
        }

        return File.Exists(_shortcutsPath)
            ? File.ReadAllBytes(_shortcutsPath)
            : ReadOnlyMemory<byte>.Empty;
    }
}
