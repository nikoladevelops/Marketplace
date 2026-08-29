// Real-time chat for the Thread page.
// We use SignalR only, no normal form posts. The connection heals itself,
// it keeps retrying forever, and we pull missed messages on every
// reconnect and whenever you switch back to the tab. That way you never
// lose a message even if your internet dropped for a bit.

(function () {
    "use strict";

    var cfg = window.chatConfig || {};
    var list = document.getElementById("messageList");

    if (!list) {
        return;
    }

    if (typeof signalR === "undefined") {
        // The chat library did not load. Show a clear message instead of failing quietly.
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

    // Keep track of the newest message we have, so we can ask for anything newer.
    var lastMessageId = 0;

    list.querySelectorAll("[data-msg-id]").forEach(function (row) {
        var id = parseInt(row.dataset.msgId, 10) || 0;

        if (id > lastMessageId) {
            lastMessageId = id;
        }
    });

    // setStatus
    // Updates the little "connected / reconnecting" badge at the top.
    function setStatus(state) {
        if (!statusEl) {
            return;
        }

        statusEl.hidden = false;

        statusEl.classList.remove("chat-status-connecting", "chat-status-online", "chat-status-offline");

        if (state === "online") {
            statusEl.classList.add("chat-status-online");
            // This is about our socket, not about the other person being online.
            statusEl.textContent = "connected";
        } else if (state === "offline") {
            statusEl.classList.add("chat-status-offline");
            statusEl.textContent = "reconnecting...";
        } else {
            statusEl.classList.add("chat-status-connecting");
            statusEl.textContent = "connecting...";
        }
    }

    // scrollToBottom
    // Keeps the chat scrolled to the newest message.
    function scrollToBottom() {
        var sc = document.getElementById("chatScroll");

        if (sc) {
            sc.scrollTop = sc.scrollHeight;
        }
    }

    // appendMessage
    // Adds one message bubble to the list. Uses textContent so HTML in
    // the message cannot run as code.
    function appendMessage(m, isMine) {
        var existing = null;

        if (m.id) {
            existing = list.querySelector('[data-msg-id="' + m.id + '"]');
        }

        if (existing) {
            return;
        }

        var row = document.createElement("div");

        if (isMine) {
            row.className = "d-flex justify-content-end mb-2";
        } else {
            row.className = "d-flex justify-content-start mb-2";
        }

        if (m.id) {
            row.dataset.msgId = m.id;
        }

        var bubble = document.createElement("div");

        if (isMine) {
            bubble.className = "chat-bubble chat-mine";
        } else {
            bubble.className = "chat-bubble chat-theirs";
        }

        var bodyDiv = document.createElement("div");
        bodyDiv.className = "chat-body";
        bodyDiv.textContent = m.body;

        var timeDiv = document.createElement("div");
        timeDiv.className = "chat-time";

        if (isMine) {
            timeDiv.textContent = fmtTime(new Date(m.sentAt)) + " \u2713";
        } else {
            timeDiv.textContent = fmtTime(new Date(m.sentAt));
        }

        if (isMine) {
            timeDiv.dataset.read = "false";
        } else {
            timeDiv.dataset.read = "n/a";
        }

        bubble.appendChild(bodyDiv);
        bubble.appendChild(timeDiv);

        row.appendChild(bubble);
        list.appendChild(row);

        if (m.id && m.id > lastMessageId) {
            lastMessageId = m.id;
        }

        scrollToBottom();

        if (emptyState && emptyState.parentNode) {
            emptyState.parentNode.removeChild(emptyState);
        }
    }

    // fmtTime
    // Formats a date like "12 Jan 14:05".
    function fmtTime(d) {
        function p(n) {
            if (n < 10) {
                return "0" + n;
            }

            return String(n);
        }

        var months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

        return p(d.getDate()) + " " + months[d.getMonth()] + " " + p(d.getHours()) + ":" + p(d.getMinutes());
    }

    // markRowAsRead
    // Turns a single check into a double check when your message was read.
    function markRowAsRead(messageId) {
        var row = list.querySelector('[data-msg-id="' + messageId + '"]');
        var timeEl = null;

        if (row) {
            timeEl = row.querySelector(".chat-time");
        }

        if (timeEl && timeEl.dataset.read === "false") {
            timeEl.textContent = timeEl.textContent.replace(/\s*\u2713\u2713?\s*$/, "") + " \u2713\u2713";
            timeEl.dataset.read = "true";
        }
    }

    // markAllMineAsRead
    // Marks every outgoing message as read. Used when the other person
    // opens the thread and triggers the MessagesRead event.
    function markAllMineAsRead() {
        list.querySelectorAll('.chat-time[data-read="false"]').forEach(function (el) {
            el.textContent = el.textContent.replace(/\s*\u2713\u2713?\s*$/, "") + " \u2713\u2713";
            el.dataset.read = "true";
        });
    }

    // syncMissing
    // Pulls any messages we missed while we were disconnected.
    function syncMissing() {
        if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
            return;
        }

        connection.invoke("GetMessagesSince", cfg.adId, cfg.partnerName, lastMessageId)
            .then(function (missed) {
                var listToUse = missed || [];

                listToUse.forEach(function (m) {
                    var isMine = m.senderName === cfg.myUserName;

                    appendMessage(m, isMine);

                    if (isMine && m.isReadByReceiver) {
                        markRowAsRead(m.id);
                    }
                });
            })
            .catch(function (err) {
                logError("GetMessagesSince", err);
            });
    }

    // scheduleSafetyNet
    // Runs syncMissing every 20 seconds as a backup, in case we missed
    // a push for any reason.
    function scheduleSafetyNet() {
        if (syncTimer) {
            clearInterval(syncTimer);
        }

        syncTimer = setInterval(syncMissing, 20000);
    }

    // We prefer WebSockets and fall back to Long Polling.
    // ServerSentEvents is skipped on purpose because it often half-connects
    // behind proxies and leaves the hub in a stuck state.
    var TRANSPORTS = signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling;

    var consecutiveFailures = 0;

    // buildConnection
    // Creates a new SignalR connection and wires up the hub handlers.
    function buildConnection() {
        var c = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/chat", { transport: TRANSPORTS })
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        c.on("ReceiveMessage", function (data) {
            var isMine = data.senderName === cfg.myUserName;

            appendMessage(data, isMine);

            // If we got a live message from the other person, we are clearly
            // looking at the thread, so mark it read right away.
            if (!isMine && c.state === signalR.HubConnectionState.Connected) {
                c.invoke("MarkThreadRead", cfg.adId, cfg.partnerName).catch(function (err) {
                    logError("MarkThreadRead", err);
                });
            }
        });

        c.on("MessagesRead", function (data) {
            if (data.byUserName === cfg.myUserName) {
                return;
            }

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

    // startConnection
    // Tries to start the connection. On failure it backs off and retries.
    function startConnection() {
        setStatus("connecting");

        // Always build a fresh connection. Old ones can stay dead after a server restart.
        connection = buildConnection();

        connection.start()
            .then(function () {
                retryDelayMs = 2000;
                consecutiveFailures = 0;
                onConnectedRecovered();
            })
            .catch(function (err) {
                consecutiveFailures++;

                logError("start", err);

                setStatus("offline");
                setOfflineUi();

                if (consecutiveFailures === 1) {
                    showChatError("Cannot reach the chat hub: " + (err.message || "start failed") +
                        ". Check the app is running (http://localhost:5256) and, if using HTTPS, that the dev certificate is trusted.");
                }

                scheduleRetry();
            });
    }

    // scheduleRetry
    // Waits a bit and then tries to reconnect. Delay doubles each time.
    function scheduleRetry() {
        setTimeout(startConnection, retryDelayMs);

        retryDelayMs = Math.min(retryDelayMs * 2, RETRY_DELAY_MAX_MS);
    }

    // onConnectedRecovered
    // Called right after we get connected. Re-joins the thread and syncs.
    function onConnectedRecovered() {
        setStatus("online");
        setOnlineUi();

        connection.invoke("JoinThread", cfg.adId, cfg.partnerName)
            .then(syncMissing)
            .catch(function (err) {
                logError("JoinThread", err);
                syncMissing();
            });
    }

    // setOnlineUi
    // Enables the input when we are online.
    function setOnlineUi() {
        if (input) {
            input.disabled = false;
        }

        setSending(false);
    }

    // setOfflineUi
    // We keep the input enabled so you can keep typing while we reconnect.
    // The send button is disabled until the socket is back.
    function setOfflineUi() {
        setSending(true);
    }

    // setSending
    // Shows a little loading state on the send button.
    function setSending(sending) {
        if (!sendButton) {
            return;
        }

        if (sending) {
            sendButton.disabled = true;
            sendButton.textContent = "...";
        } else {
            sendButton.disabled = false;
            sendButton.textContent = "Send \u27A4";
        }
    }

    // showChatError
    // Pops a small toast with an error. Auto hides after a few seconds.
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

        toast.textContent = "\u26A0\uFE0F " + text;
        toast.hidden = false;
        toast.style.display = "block";

        clearTimeout(showChatError._timer);

        showChatError._timer = setTimeout(function () {
            toast.hidden = true;
            toast.style.display = "none";
        }, 4200);
    }

    // logError
    // Small wrapper so we can turn logging on/off in one place.
    function logError(where, err) {
        if (console && console.error) {
            console.error("[chat] " + where, err);
        }
    }

    // Phone quick send - one click to share your phone number.
    // The button just submits the hidden form, so it works even if SignalR is down.
    var phoneBtn = document.getElementById("sendPhoneBtn");
    var phoneForm = document.getElementById("sendPhoneForm");

    if (phoneBtn && phoneForm) {
        phoneBtn.addEventListener("click", function () {
            phoneForm.requestSubmit();
        });
    }

    // Sending - only through the hub, no normal form post.
    if (form) {
        form.addEventListener("submit", function (e) {
            e.preventDefault();

            if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
                showChatError("Connection lost - reconnecting. Your message is kept; try again in a moment.");
                return;
            }

            var text = input.value.trim();

            if (!text) {
                return;
            }

            setSending(true);

            connection.invoke("SendMessage", cfg.adId, cfg.partnerName, text)
                .then(function () {
                    input.value = "";
                    setSending(false);
                })
                .catch(function (err) {
                    setSending(false);
                    showChatError(err.message || "Could not send the message.");
                    logError("SendMessage", err);
                });
        });
    }

    // When you come back to the tab, check for new messages right away.
    document.addEventListener("visibilitychange", function () {
        if (document.visibilityState === "visible") {
            syncMissing();
        }
    });

    startConnection();
    scheduleSafetyNet();
})();
