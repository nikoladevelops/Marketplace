// Navbar unread badge - keeps the little red number in the top nav up to date.
// Uses the same SignalR hub as the inbox and thread pages.

(function () {
    "use strict";

    var link = document.getElementById("navbarMessagesLink");
    var badge = document.getElementById("navbarUnreadBadge");

    if (!link || !badge) {
        return;
    }

    if (typeof signalR === "undefined") {
        return;
    }

    var myUser = (link.dataset.username || "").trim();

    if (!myUser) {
        return;
    }

    var TRANSPORTS = signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling;
    var connection = null;
    var retryMs = 2000;
    var MAX_MS = 30000;

    // setBadge
    // Sets the number in the red bubble. Hides it when the count is zero.
    function setBadge(count) {
        var n = parseInt(count, 10) || 0;

        if (n > 99) {
            badge.textContent = "99+";
        } else {
            badge.textContent = String(n);
        }

        if (n <= 0) {
            badge.classList.add("d-none");
        } else {
            badge.classList.remove("d-none");
        }

        link.dataset.unread = String(n);
    }

    // incBadge
    // Bumps the badge by one. Used when a live message arrives.
    function incBadge() {
        var cur = parseInt(badge.textContent, 10) || 0;

        if (badge.classList.contains("d-none")) {
            cur = 0;
        }

        setBadge(cur + 1);
    }

    // syncBadge
    // Asks the server for the real unread count. Good for cleaning up
    // after you read messages in another tab.
    function syncBadge() {
        fetch("/Chat/UnreadCount", { headers: { "X-Requested-With": "XMLHttpRequest" } })
            .then(function (r) {
                return r.json();
            })
            .then(function (j) {
                if (j && typeof j.count !== "undefined") {
                    setBadge(j.count);
                }
            })
            .catch(function () {});
    }

    // build
    // Creates a hub connection and listens for new messages and read events.
    function build() {
        var c = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/chat", { transport: TRANSPORTS })
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        c.on("ReceiveMessage", function (data) {
            if (!data) {
                return;
            }

            if (data.senderName === myUser) {
                return;
            }

            incBadge();
        });

        c.on("MessagesRead", function () {
            // Wait a tiny bit and then sync, so we do not hammer the server
            // if several tabs fire this at once.
            clearTimeout(build._syncT);

            build._syncT = setTimeout(syncBadge, 300);
        });

        c.onclose(function () {
            schedule();
        });

        return c;
    }

    // start
    // Starts the connection. If it fails we retry with backoff.
    function start() {
        connection = build();

        connection.start().then(function () {
            retryMs = 2000;
        }).catch(function (e) {
            if (console && console.error) {
                console.error("[navbar] start", e);
            }

            schedule();
        });
    }

    // schedule
    // Waits a bit and tries to connect again. Each failure waits longer.
    function schedule() {
        setTimeout(start, retryMs);

        retryMs = Math.min(retryMs * 2, MAX_MS);
    }

    start();
})();
