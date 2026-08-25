// Navbar live unread badge + toast — same SignalR user group as Inbox/Thread.
// Keeps top nav instantly in sync without page reload. Toast is responsive (92vw on mobile).
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

    function showToast(data) {
        var toast = document.getElementById("navbarToast");
        if (!toast) {
            toast = document.createElement("div");
            toast.id = "navbarToast";
            toast.className = "alert chat-toast-beautiful rounded-4";
            toast.setAttribute("role", "alert");
            toast.style.cursor = "pointer";
            toast.addEventListener("click", function () {
                var url = "/Chat/Thread?with=" + encodeURIComponent(data.senderName) + "&adId=" + encodeURIComponent(data.advertisementId);
                window.location.href = url;
            });
            document.body.appendChild(toast);
        }
        var snippet = (data.body || "").length > 56 ? data.body.substring(0, 56) + "…" : data.body;
        toast.innerHTML = '<div class="d-flex align-items-center gap-2"><span class="flex-shrink-0" style="font-size:1.1rem;">💬</span><div class="flex-grow-1 min-width-0"><div class="fw-semibold small text-truncate">New message from ' + escapeHtml(data.senderName) + '</div><div class="small text-muted text-truncate">' + escapeHtml(snippet) + '</div></div><span class="badge bg-primary flex-shrink-0">Open</span></div>';
        toast.hidden = false;
        toast.style.display = "block";
        clearTimeout(showToast._t);
        showToast._t = setTimeout(function () { toast.hidden = true; toast.style.display = "none"; }, 5500);
    }
    function escapeHtml(s) {
        var d = document.createElement("div");
        d.textContent = s || "";
        return d.innerHTML;
    }

    function build() {
        var c = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/chat", { transport: TRANSPORTS })
            .configureLogging(signalR.LogLevel.Warning)
            .build();
        c.on("ReceiveMessage", function (data) {
            if (!data || data.senderName === myUser) return;
            incBadge();
            // Only toast if not already on that Thread (chat.js will handle thread UI)
            var onSameThread = window.chatConfig && window.chatConfig.partnerName === data.senderName && String(window.chatConfig.adId) === String(data.advertisementId);
            if (!onSameThread) showToast(data);
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
