define(['baseView', 'loading', 'emby-input', 'emby-button', 'emby-scroller'], function (BaseView, loading) {
    'use strict';

    var pluginId = '57044f85-39b9-4aa8-b4c8-058992a6e49e';

    function loadConfig(view) {
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            view.querySelector('.txtTmdbReadAccessToken').value = config.TmdbReadAccessToken || '';
            loading.hide();
        }, function () {
            loading.hide();
        });
    }

    function saveConfig(view) {
        loading.show();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            config.TmdbReadAccessToken = view.querySelector('.txtTmdbReadAccessToken').value.trim();
            return ApiClient.updatePluginConfiguration(pluginId, config);
        }).then(function (result) {
            Dashboard.processPluginConfigurationUpdateResult(result);
            loading.hide();
        }, function () {
            loading.hide();
        });
    }

    function View(view) {
        BaseView.apply(this, arguments);

        view.querySelector('.coverFixerConfigForm').addEventListener('submit', function (event) {
            event.preventDefault();
            saveConfig(view);
            return false;
        });
    }

    Object.assign(View.prototype, BaseView.prototype);

    View.prototype.onResume = function () {
        BaseView.prototype.onResume.apply(this, arguments);
        loading.show();
        loadConfig(this.view);
    };

    return View;
});
