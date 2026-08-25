// Live Inbox: unread badges + snippets update in real time via the ChatHub user group.
// Connection is self-healing: retries forever with backoff, so a laptop that slept
// resumes live updates on wake without a manual refresh.
(function () {
    "use strict";

    var root = document.getElementById("inboxRoot");
    if (!root || typeof signalR === "undefined") return;

    var myUserName = root.dataset.myUserName;
    var connection = null;
    var retryDelayMs = 2000;
    var RETRY_DELAY_MAX_MS = 30000;

    // Same transport policy as chat.js: WebSockets first, Long Polling fallback,
    // no Server-Sent Events (prone to silent half-connects behind proxies).
    var TRANSPORTS = signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling;

    function buildConnection() {
        var c = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/chat", { transport: TRANSPORTS })
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        c.on("ReceiveMessage", function (data) {
            if (data.senderName === myUserName) return; // my own outgoing message

            var key = (data.advertisementId || "") + "|" + data.senderName;
            var snippet = document.querySelector('.inbox-snippet[data-key="' + cssEscape(key) + '"]');
            var row = snippet ? snippet.closest(".inbox-row") : null;

            // Conversation not on the page yet (new thread or on another inbox page) → go to first page to show it.
            if (!row) {
                // If paginated away from page 1, jump to inbox root; otherwise simple reload.
                var isPaginated = window.location.search.indexOf("page=") !== -1;
                if (isPaginated) {
                    window.location.href = window.location.pathname;
                } else {
                    window.location.reload();
                }
                return;
            }

            // Move the conversation to the top of the list.
            var card = row.parentNode;
            if (card.firstElementChild !== row) {
                card.insertBefore(row, card.firstElementChild);
            }

            snippet.textContent = data.body;
            updateBadge(row, key);
        });

        c.on("MessagesRead", function (data) {
            // My badges clear when I opened the thread elsewhere (the broadcast
            // carries the reader's name).
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

    function startConnection() {
        // Rebuild each attempt so a stale connection object never lingers.
        connection = buildConnection();
        connection.start().then(function () {
            retryDelayMs = 2000; // reset backoff after success
        }).catch(function (err) {
            if (console && console.error) console.error("[chat-inbox] start", err);
            scheduleRetry();
        });
    }

    function scheduleRetry() {
        setTimeout(startConnection, retryDelayMs);
        retryDelayMs = Math.min(retryDelayMs * 2, RETRY_DELAY_MAX_MS);
    }

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

    function cssEscape(value) {
        if (window.CSS && CSS.escape) return CSS.escape(value);
        return value.replace(/"/g, '\\"');
    }

    startConnection();
})();
