// Live Inbox - keeps the inbox rows fresh without a reload.
// Uses the same SignalR group as the thread page, so new messages
// pop to the top instantly. The connection retries forever.

(function () {
    "use strict";

    var root = document.getElementById("inboxRoot");

    if (!root) {
        return;
    }

    if (typeof signalR === "undefined") {
        return;
    }

    var myUserName = root.dataset.myUserName;
    var connection = null;
    var retryDelayMs = 2000;
    var RETRY_DELAY_MAX_MS = 30000;

    // We try WebSockets first, then Long Polling.
    // SSE is skipped because it can look connected but not actually push.
    var TRANSPORTS = signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling;

    // buildConnection
    // Creates the hub connection and sets up the inbox handlers.
    function buildConnection() {
        var c = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/chat", { transport: TRANSPORTS })
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        c.on("ReceiveMessage", function (data) {
            if (data.senderName === myUserName) {
                return;
            }

            var key = (data.advertisementId || "") + "|" + data.senderName;
            var snippet = document.querySelector('.inbox-snippet[data-key="' + cssEscape(key) + '"]');
            var row = null;

            if (snippet) {
                row = snippet.closest(".inbox-row");
            }

            // New conversation we did not have on this page.
            if (!row) {
                var isPaginated = window.location.search.indexOf("page=") !== -1;

                if (isPaginated) {
                    window.location.href = window.location.pathname;
                } else {
                    window.location.reload();
                }

                return;
            }

            // Move this chat to the top so the newest is first.
            var card = row.parentNode;

            if (card.firstElementChild !== row) {
                card.insertBefore(row, card.firstElementChild);
            }

            snippet.textContent = data.body;

            updateBadge(row, key);
        });

        c.on("MessagesRead", function (data) {
            // Someone read the messages, so clear our unread badge.
            document.querySelectorAll(".unread-badge").forEach(function (badge) {
                var row = badge.closest(".inbox-row");

                if (row && row.dataset.partner === data.byUserName) {
                    badge.remove();
                }
            });
        });

        c.onclose(function () {
            scheduleRetry();
        });

        return c;
    }

    // startConnection
    // Connects to the hub. Builds a fresh connection each time so we
    // never reuse a dead object after a server restart.
    function startConnection() {
        connection = buildConnection();

        connection.start().then(function () {
            retryDelayMs = 2000;
        }).catch(function (err) {
            if (console && console.error) {
                console.error("[chat-inbox] start", err);
            }

            scheduleRetry();
        });
    }

    // scheduleRetry
    // Waits a bit, then tries again. The wait time grows with each failure.
    function scheduleRetry() {
        setTimeout(startConnection, retryDelayMs);

        retryDelayMs = Math.min(retryDelayMs * 2, RETRY_DELAY_MAX_MS);
    }

    // updateBadge
    // Makes sure the red unread bubble exists and bumps the number by one.
    function updateBadge(row, key) {
        var badge = row.querySelector(".unread-badge");

        if (!badge) {
            badge = document.createElement("span");
            badge.className = "badge rounded-pill bg-danger flex-shrink-0 unread-badge";
            badge.dataset.key = key;
            badge.textContent = "0";

            var nameSpan = row.querySelector("span.fw-semibold");

            if (nameSpan && nameSpan.parentNode) {
                nameSpan.parentNode.insertBefore(badge, nameSpan.nextSibling);
            }
        }

        var current = parseInt(badge.textContent, 10) || 0;

        badge.textContent = String(current + 1);
    }

    // cssEscape
    // Escapes a string for use inside a CSS selector. Uses the built in
    // CSS.escape when available.
    function cssEscape(value) {
        if (window.CSS && CSS.escape) {
            return CSS.escape(value);
        }

        return value.replace(/"/g, '\\"');
    }

    startConnection();
})();
