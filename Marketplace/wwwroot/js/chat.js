// Real-time chat client for the Chat/Thread page.
// Transport: SignalR only — there is no form-postback path. The connection is
// self-healing: it retries forever with backoff, and every (re)connect plus tab
// focus triggers GetMessagesSince catch-up, so no message can ever be missed
// even if the socket was down at the moment the peer hit Send.
(function () {
    "use strict";

    var cfg = window.chatConfig || {};
    var list = document.getElementById("messageList");
    if (!list) return;

    if (typeof signalR === "undefined") {
        // The library failed to load — make that visible instead of dying silently.
        var libAlert = document.createElement("div");
        libAlert.className = "alert alert-danger rounded-4 border-0";
        libAlert.textContent = "Chat library failed to load. Please hard-refresh the page (Ctrl+Shift+R).";
        list.appendChild(libAlert);
        return;
    }

    var statusEl = document.getElementById("connStatus");
    var form = document.getElementById("chatForm");
    var input = document.getElementById("messageInput");
    var sendButton = document.getElementById("sendButton");
    var emptyState = document.getElementById("emptyState");

    var connection = null;
    var retryDelayMs = 2000;
    var RETRY_DELAY_MAX_MS = 30000;
    var syncTimer = null;

    // Newest message id we already render; everything above it gets pulled via sync.
    var lastMessageId = 0;
    list.querySelectorAll("[data-msg-id]").forEach(function (row) {
        lastMessageId = Math.max(lastMessageId, parseInt(row.dataset.msgId, 10) || 0);
    });

    function setStatus(state) {
        if (!statusEl) return;
        statusEl.hidden = false;
        statusEl.classList.remove("chat-status-connecting", "chat-status-online", "chat-status-offline");
        if (state === "online") {
            statusEl.classList.add("chat-status-online");
            statusEl.textContent = "● online";
        } else if (state === "offline") {
            statusEl.classList.add("chat-status-offline");
            statusEl.textContent = "● offline — reconnecting…";
        } else {
            statusEl.classList.add("chat-status-connecting");
            statusEl.textContent = "connecting…";
        }
    }

    function scrollToBottom() {
        var sc = document.getElementById("chatScroll");
        if (sc) sc.scrollTop = sc.scrollHeight;
    }

    // textContent (not innerHTML) keeps user-provided text XSS-safe.
    function appendMessage(m, isMine) {
        var existing = m.id ? list.querySelector('[data-msg-id="' + m.id + '"]') : null;
        if (existing) return;

        var row = document.createElement("div");
        row.className = "d-flex " + (isMine ? "justify-content-end" : "justify-content-start") + " mb-2";
        if (m.id) row.dataset.msgId = m.id;

        var bubble = document.createElement("div");
        bubble.className = "chat-bubble " + (isMine ? "chat-mine" : "chat-theirs");

        var bodyDiv = document.createElement("div");
        bodyDiv.className = "chat-body";
        bodyDiv.textContent = m.body;

        var timeDiv = document.createElement("div");
        timeDiv.className = "chat-time";
        timeDiv.textContent = fmtTime(new Date(m.sentAt)) + (isMine ? " ✓" : "");
        timeDiv.dataset.read = isMine ? "false" : "n/a";

        bubble.appendChild(bodyDiv);
        bubble.appendChild(timeDiv);
        row.appendChild(bubble);
        list.appendChild(row);

        if (m.id) lastMessageId = Math.max(lastMessageId, m.id);

        scrollToBottom();

        if (emptyState && emptyState.parentNode) {
            emptyState.parentNode.removeChild(emptyState);
        }
    }

    function fmtTime(d) {
        function p(n) { return (n < 10 ? "0" : "") + n; }
        return p(d.getDate()) + " " +
            ["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"][d.getMonth()] +
            " " + p(d.getHours()) + ":" + p(d.getMinutes());
    }

    function markRowAsRead(messageId) {
        var row = list.querySelector('[data-msg-id="' + messageId + '"]');
        var timeEl = row ? row.querySelector(".chat-time") : null;
        if (timeEl && timeEl.dataset.read === "false") {
            timeEl.textContent = timeEl.textContent.replace(/\s*✓✓?\s*$/, "") + " ✓✓";
            timeEl.dataset.read = "true";
        }
    }

    function markAllMineAsRead() {
        list.querySelectorAll('.chat-time[data-read="false"]').forEach(function (el) {
            el.textContent = el.textContent.replace(/\s*✓✓?\s*$/, "") + " ✓✓";
            el.dataset.read = "true";
        });
    }

    // ---------- catch-up sync: pulls anything missed while disconnected ----------

    function syncMissing() {
        if (!connection || connection.state !== signalR.HubConnectionState.Connected) return;
        connection.invoke("GetMessagesSince", cfg.adId, cfg.partnerName, lastMessageId)
            .then(function (missed) {
                (missed || []).forEach(function (m) {
                    var isMine = m.senderName === cfg.myUserName;
                    appendMessage(m, isMine);
                    // A message of mine that was already read while I was offline.
                    if (isMine && m.isReadByReceiver) markRowAsRead(m.id);
                });
            })
            .catch(function (err) { logError("GetMessagesSince", err); });
    }

    function scheduleSafetyNet() {
        if (syncTimer) clearInterval(syncTimer);
        syncTimer = setInterval(syncMissing, 20000);
    }

    // ---------- infinite self-healing connection loop ----------

    // WebSockets preferred, Long Polling as the only fallback. Server-Sent Events
    // is skipped on purpose: behind proxies/dev setups it is the transport that most
    // often half-connects (negotiate OK, stream stalls), leaving the hub stuck
    // between Connecting and Connected so every Send hits the "connection lost" guard.
    var TRANSPORTS = signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling;

    var consecutiveFailures = 0;

    function buildConnection() {
        var c = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/chat", { transport: TRANSPORTS })
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        c.on("ReceiveMessage", function (data) {
            var isMine = data.senderName === cfg.myUserName;
            appendMessage(data, isMine);
            // A live incoming message means I am looking at the thread; acknowledge instantly.
            if (!isMine && c.state === signalR.HubConnectionState.Connected) {
                c.invoke("MarkThreadRead", cfg.adId, cfg.partnerName).catch(function (err) {
                    logError("MarkThreadRead", err);
                });
            }
        });

        c.on("MessagesRead", function (data) {
            if (data.byUserName === cfg.myUserName) return; // ignore my own echo
            markAllMineAsRead();
        });

        c.onclose(function (error) {
            setStatus("offline");
            setOfflineUi();
            if (error) {
                logError("onclose", error);
                showChatError("Chat connection dropped: " + (error.message || "unknown reason"));
            }
            scheduleRetry();
        });

        return c;
    }

    function startConnection() {
        setStatus("connecting");
        // A page left open across a server restart keeps a dead connection object;
        // rebuilding on every attempt guarantees a fresh negotiate + transport.
        connection = buildConnection();

        connection.start()
            .then(function () {
                retryDelayMs = 2000; // reset backoff after success
                consecutiveFailures = 0;
                onConnectedRecovered();
            })
            .catch(function (err) {
                consecutiveFailures++;
                logError("start", err);
                setStatus("offline");
                setOfflineUi();
                if (consecutiveFailures === 1) {
                    // On Linux dev default is http://localhost:5256 (no cert needed).
                    // If you re-enable https://localhost:7256, trust the dev cert: `dotnet dev-certs https --trust`.
                    showChatError("Cannot reach the chat hub: " + (err.message || "start failed") +
                        ". Check the app is running (http://localhost:5256) and, if using HTTPS, that the dev certificate is trusted.");
                }
                scheduleRetry();
            });
    }

    function scheduleRetry() {
        setTimeout(startConnection, retryDelayMs);
        retryDelayMs = Math.min(retryDelayMs * 2, RETRY_DELAY_MAX_MS);
    }

    function onConnectedRecovered() {
        setStatus("online");
        setOnlineUi();
        // Re-register thread context and pull anything missed while offline.
        connection.invoke("JoinThread", cfg.adId, cfg.partnerName)
            .then(syncMissing)
            .catch(function (err) { logError("JoinThread", err); syncMissing(); });
    }

    function setOnlineUi() {
        if (input) input.disabled = false;
        setSending(false);
    }

    function setOfflineUi() {
        // Input stays enabled so the user can keep typing while we reconnect;
        // submit is guarded and preserves the text.
        setSending(true);
    }

    function setSending(sending) {
        if (!sendButton) return;
        sendButton.disabled = sending;
        sendButton.textContent = sending ? "…" : "Send ➤";
    }

    function showChatError(text) {
        var toast = document.getElementById("chatToast");
        if (!toast) {
            toast = document.createElement("div");
            toast.id = "chatToast";
            toast.className = "alert chat-toast-beautiful rounded-4 border-0 shadow";
            toast.setAttribute("role", "alert");
            toast.style.position = "fixed";
            document.body.appendChild(toast);
        }
        toast.textContent = "⚠️ " + text;
        toast.hidden = false;
        toast.style.display = "block";
        clearTimeout(showChatError._timer);
        showChatError._timer = setTimeout(function () { toast.hidden = true; toast.style.display = "none"; }, 4200);
    }

    function logError(where, err) {
        if (console && console.error) console.error("[chat] " + where, err);
    }

    // ---------- sending (hub-only; no form postback exists) ----------

    if (form) {
        form.addEventListener("submit", function (e) {
            e.preventDefault(); // never post the form anywhere

            if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
                showChatError("Connection lost — reconnecting. Your message is kept; try again in a moment.");
                return;
            }

            var text = input.value.trim();
            if (!text) return;

            setSending(true);
            connection.invoke("SendMessage", cfg.adId, cfg.partnerName, text)
                .then(function () {
                    input.value = "";
                    setSending(false);
                })
                .catch(function (err) {
                    setSending(false);
                    // Hub rejections carry user-facing reasons (validation, blocks).
                    showChatError(err.message || "Could not send the message.");
                    logError("SendMessage", err);
                });
        });
    }

    // Tab regains focus → pull anything that arrived while backgrounded.
    document.addEventListener("visibilitychange", function () {
        if (document.visibilityState === "visible") syncMissing();
    });

    startConnection();
    scheduleSafetyNet();
})();
