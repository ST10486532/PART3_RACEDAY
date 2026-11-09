// Small helpers for RaceDay pages.
(function () {
    // Auto-dismiss success alerts after a few seconds (errors and warnings stay until closed).
    document.querySelectorAll('.alert-success.rd-alert').forEach(function (alert) {
        setTimeout(function () {
            if (window.bootstrap && bootstrap.Alert) {
                bootstrap.Alert.getOrCreateInstance(alert).close();
            }
        }, 6000);
    });
})();
