// Recently Browsed — localStorage-backed horizontal scroller for the Home page.
// Designed to be safe in the face of bad data: corrupt JSON, missing fields,
// quota exhaustion, localStorage being disabled, and old schema versions.
//
// Public API (exposed on window.RecentlyViewed):
//   - record(ad)              -> void    add an ad to the history
//   - render(container)       -> void    paint the strip into the given element
//   - clear()                 -> void    wipe the history
//   - list(currentUserId?)    -> RecentAd[]  read + validate + filter

(function () {
    "use strict";

    var STORAGE_KEY = "mb.recentAds";
    var MAX_ITEMS = 50;
    var FALLBACK_IMG = "/plusSign.png";

    // Feature-detect localStorage once at module init. Some private-browsing
    // modes throw on access; some return null. Either way, the module becomes
    // a no-op so the rest of the page keeps working.
    var hasStorage = (function () {
        try {
            var probe = "__mb_recentlyViewed_probe__";
            window.localStorage.setItem(probe, "1");
            window.localStorage.removeItem(probe);
            return true;
        } catch (e) {
            return false;
        }
    })();

    function readRaw() {
        if (!hasStorage) return null;
        var raw = window.localStorage.getItem(STORAGE_KEY);
        if (raw == null) return null;
        try {
            var parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed : null;
        } catch (e) {
            // Corrupt JSON. Wipe so subsequent reads succeed.
            try { window.localStorage.removeItem(STORAGE_KEY); } catch (_) {}
            return null;
        }
    }

    function writeRaw(items) {
        if (!hasStorage) return false;
        var payload = JSON.stringify(items);

        function attempt(value) {
            try {
                window.localStorage.setItem(STORAGE_KEY, value);
                return true;
            } catch (e) {
                return false;
            }
        }

        if (attempt(payload)) return true;

        // Quota exceeded (or some other storage error). Drop the oldest half
        // of the list and retry. Bounded so a single call can't loop forever.
        if (items.length > 4) {
            var trimmed = items.slice(0, Math.max(1, Math.floor(items.length / 2)));
            return attempt(JSON.stringify(trimmed));
        }
        return false;
    }

    // Schema validation: keep entries that look like valid recent ads.
    // Defensive against old versions and bad writes.
    function isValidItem(x) {
        if (!x || typeof x !== "object") return false;
        if (typeof x.id !== "number" || !isFinite(x.id) || x.id <= 0) return false;
        if (typeof x.title !== "string") return false;
        if (typeof x.price !== "string") return false;
        if (typeof x.imagePath !== "string") return false;
        if (typeof x.userId !== "string") return false;
        if (typeof x.userName !== "string") return false;
        if (typeof x.viewedAt !== "number" || !isFinite(x.viewedAt)) return false;
        // Reject timestamps in the far future (clock skew, bad data).
        if (x.viewedAt > Date.now() + 24 * 60 * 60 * 1000) return false;
        return true;
    }

    function normalize(x) {
        return {
            id: x.id,
            title: String(x.title),
            price: String(x.price),
            imagePath: String(x.imagePath),
            userId: String(x.userId),
            userName: String(x.userName),
            viewedAt: Number(x.viewedAt)
        };
    }

    function list(currentUserId) {
        var raw = readRaw();
        if (!raw) return [];
        var seen = Object.create(null);
        var out = [];
        for (var i = 0; i < raw.length; i++) {
            var item = raw[i];
            if (!isValidItem(item)) continue;
            if (seen[item.id]) continue; // dedupe across the persisted list
            // Hide the viewer's own ads from their own history.
            if (currentUserId && item.userId === currentUserId) continue;
            seen[item.id] = true;
            out.push(normalize(item));
            if (out.length >= MAX_ITEMS) break;
        }
        return out;
    }

    function record(ad) {
        if (!ad || typeof ad !== "object") return;
        if (typeof ad.id !== "number" || !isFinite(ad.id) || ad.id <= 0) return;
        var item = {
            id: ad.id,
            title: typeof ad.title === "string" ? ad.title : "",
            price: typeof ad.price === "string" ? ad.price : "",
            imagePath: typeof ad.imagePath === "string" ? ad.imagePath : "",
            userId: typeof ad.userId === "string" ? ad.userId : "",
            userName: typeof ad.userName === "string" ? ad.userName : "",
            viewedAt: Date.now()
        };

        var existing = readRaw() || [];
        var next = [item];
        for (var i = 0; i < existing.length; i++) {
            var cur = existing[i];
            if (!isValidItem(cur)) continue;
            if (cur.id === item.id) continue; // dedupe — new view bumps to front
            next.push(cur);
            if (next.length >= MAX_ITEMS) break;
        }
        writeRaw(next);
    }

    function clear() {
        if (!hasStorage) return;
        try { window.localStorage.removeItem(STORAGE_KEY); } catch (_) {}
    }

    function escapeAttr(s) {
        return String(s)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    // Build the strip DOM. We use createElement + textContent to avoid
    // injecting any user data as HTML.
    function buildCard(item) {
        var link = document.createElement("a");
        link.href = "/Advertisement/Show/" + item.id;
        link.className = "recent-card text-decoration-none";

        var img = document.createElement("img");
        img.className = "recent-card-img";
        img.loading = "lazy";
        img.alt = escapeAttr(item.title || "Listing");
        img.src = item.imagePath || FALLBACK_IMG;
        img.onerror = function () { this.onerror = null; this.src = FALLBACK_IMG; };

        var title = document.createElement("div");
        title.className = "recent-card-title text-truncate";
        title.textContent = item.title || "(untitled)";

        var price = document.createElement("div");
        price.className = "recent-card-price";
        price.textContent = item.price || "";

        link.appendChild(img);
        link.appendChild(title);
        link.appendChild(price);
        return link;
    }

    function render(container) {
        if (!container) return;
        var currentUserId = container.getAttribute("data-current-user-id") || "";
        var items = list(currentUserId || null);

        // Always reset to a clean state so re-renders don't duplicate.
        container.innerHTML = "";

        if (items.length === 0) {
            container.hidden = true;
            return;
        }
        container.hidden = false;

        var header = document.createElement("div");
        header.className = "recent-header d-flex align-items-center mb-2";

        var title = document.createElement("div");
        title.className = "recent-header-title fw-semibold";
        // Safe: only static text, no user data.
        title.textContent = "Recently browsed";
        var icon = document.createElement("span");
        icon.setAttribute("aria-hidden", "true");
        icon.className = "me-2";
        icon.textContent = "🕘";
        title.insertBefore(icon, title.firstChild);

        var spacer = document.createElement("div");
        spacer.className = "flex-grow-1";

        var clearBtn = document.createElement("button");
        clearBtn.type = "button";
        clearBtn.className = "btn btn-link btn-sm recent-clear";
        clearBtn.textContent = "Clear";
        clearBtn.addEventListener("click", function () {
            clear();
            render(container);
        });

        var prevBtn = document.createElement("button");
        prevBtn.type = "button";
        prevBtn.className = "btn btn-sm btn-outline-secondary recent-nav d-none d-md-inline-flex align-items-center justify-content-center";
        prevBtn.setAttribute("aria-label", "Scroll recently browsed left");
        prevBtn.innerHTML = "&#8249;";
        prevBtn.addEventListener("click", function () {
            scrollBy(container, -1);
        });

        var nextBtn = document.createElement("button");
        nextBtn.type = "button";
        nextBtn.className = "btn btn-sm btn-outline-secondary recent-nav d-none d-md-inline-flex align-items-center justify-content-center";
        nextBtn.setAttribute("aria-label", "Scroll recently browsed right");
        nextBtn.innerHTML = "&#8250;";
        nextBtn.addEventListener("click", function () {
            scrollBy(container, 1);
        });

        var nav = document.createElement("div");
        nav.className = "d-flex gap-1";
        nav.appendChild(prevBtn);
        nav.appendChild(nextBtn);

        header.appendChild(title);
        header.appendChild(spacer);
        header.appendChild(clearBtn);
        header.appendChild(nav);

        var track = document.createElement("div");
        track.className = "recent-track";
        for (var i = 0; i < items.length; i++) {
            track.appendChild(buildCard(items[i]));
        }

        container.appendChild(header);
        container.appendChild(track);
    }

    function scrollBy(container, direction) {
        var track = container.querySelector(".recent-track");
        if (!track) return;
        var amount = Math.max(200, Math.floor(track.clientWidth * 0.8));
        track.scrollBy({ left: amount * direction, behavior: "smooth" });
    }

    // Re-render when another tab updates the key.
    window.addEventListener("storage", function (e) {
        if (e.key !== STORAGE_KEY) return;
        var el = document.getElementById("recentlyBrowsedContainer");
        if (el) render(el);
    });

    window.RecentlyViewed = {
        record: record,
        render: render,
        clear: clear,
        list: list
    };
})();
