// Navbar live unread badge — same SignalR user group as Inbox/Thread.
// Keeps top nav instantly in sync without page reload. No toast (badge is enough).
(function () {
    "use strict";
    var link = document.getElementById("navbarMessagesLink");
    var badge = document.getElementById("navbarUnreadBadge");
    if (!link || !badge || typeof signalR === "undefined") return;

    var myUser = (link.dataset.username || "").trim();
    if (!myUser) return;

    var TRANSPORTS = signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling;
    var connection = null;
    var retryMs = 2000;
    var MAX_MS = 30000;

    function setBadge(count) {
        var n = parseInt(count, 10) || 0;
        badge.textContent = n > 99 ? "99+" : String(n);
        badge.classList.toggle("d-none", n <= 0);
        link.dataset.unread = String(n);
    }
    function incBadge() {
        var cur = parseInt(badge.textContent, 10) || 0;
        if (badge.classList.contains("d-none")) cur = 0;
        setBadge(cur + 1);
    }
    function syncBadge() {
        fetch("/Chat/UnreadCount", { headers: { "X-Requested-With": "XMLHttpRequest" } })
            .then(function (r) { return r.json(); })
            .then(function (j) { if (j && typeof j.count !== "undefined") setBadge(j.count); })
            .catch(function () { });
    }

    function build() {
        var c = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/chat", { transport: TRANSPORTS })
            .configureLogging(signalR.LogLevel.Warning)
            .build();
        c.on("ReceiveMessage", function (data) {
            if (!data || data.senderName === myUser) return;
            incBadge();
        });
        c.on("MessagesRead", function () {
            // Debounce a server sync — accurate even when multiple tabs mark read
            clearTimeout(build._syncT);
            build._syncT = setTimeout(syncBadge, 300);
        });
        c.onclose(function () { schedule(); });
        return c;
    }
    function start() {
        connection = build();
        connection.start().then(function () { retryMs = 2000; }).catch(function (e) {
            if (console && console.error) console.error("[navbar] start", e);
            schedule();
        });
    }
    function schedule() {
        setTimeout(start, retryMs);
        retryMs = Math.min(retryMs * 2, MAX_MS);
    }
    start();
})();
