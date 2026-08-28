// Recently Browsed - saves the ads you clicked to localStorage
// and shows them in a horizontal strip on the home page.
// It tries to be safe when storage is disabled, when data is broken,
// or when we hit the browser storage limit.
// Rendering is done through HorizontalScroller so both this feature
// and Recommended share the same look and code.

(function () {
    "use strict";

    var STORAGE_KEY = "mb.recentAds";
    var MAX_ITEMS = 50;
    var FALLBACK_IMG = "/plusSign.png";

    // hasStorage
    // Check once at start if localStorage actually works.
    // In some private modes it throws, so we treat it as not available.
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

    // readRaw
    // Reads the raw array from localStorage. Returns null if there is
    // nothing saved, if parsing fails, or if storage is not available.
    // If the JSON is broken we clear the key so next reads can succeed.
    function readRaw() {
        if (!hasStorage) {
            return null;
        }

        var raw = window.localStorage.getItem(STORAGE_KEY);

        if (raw == null) {
            return null;
        }

        try {
            var parsed = JSON.parse(raw);

            if (Array.isArray(parsed)) {
                return parsed;
            } else {
                return null;
            }
        } catch (e) {
            try {
                window.localStorage.removeItem(STORAGE_KEY);
            } catch (_) {}

            return null;
        }
    }

    // writeRaw
    // Saves the array to localStorage. If we hit a quota error we cut
    // the list in half and try again. We only try once after trimming.
    function writeRaw(items) {
        if (!hasStorage) {
            return false;
        }

        var payload = JSON.stringify(items);

        function attempt(value) {
            try {
                window.localStorage.setItem(STORAGE_KEY, value);
                return true;
            } catch (e) {
                return false;
            }
        }

        if (attempt(payload)) {
            return true;
        }

        if (items.length > 4) {
            var trimmed = items.slice(0, Math.max(1, Math.floor(items.length / 2)));
            return attempt(JSON.stringify(trimmed));
        }

        return false;
    }

    // isValidItem
    // Checks if an object looks like a real recently viewed ad.
    // We keep it strict for required fields and relaxed for optional ones
    // so older saved items still pass.
    function isValidItem(x) {
        if (!x || typeof x !== "object") {
            return false;
        }

        if (typeof x.id !== "number" || !isFinite(x.id) || x.id <= 0) {
            return false;
        }

        if (typeof x.title !== "string") {
            return false;
        }

        if (typeof x.price !== "string") {
            return false;
        }

        if (typeof x.imagePath !== "string") {
            return false;
        }

        if (typeof x.userId !== "string") {
            return false;
        }

        if (typeof x.userName !== "string") {
            return false;
        }

        if (typeof x.viewedAt !== "number" || !isFinite(x.viewedAt)) {
            return false;
        }

        if (x.viewedAt > Date.now() + 24 * 60 * 60 * 1000) {
            return false;
        }

        // Optional extended fields. If they exist they must be valid.
        if (x.categoryId != null && (typeof x.categoryId !== "number" || !isFinite(x.categoryId) || x.categoryId <= 0)) {
            return false;
        }

        if (x.priceValue != null && typeof x.priceValue !== "number") {
            return false;
        }

        return true;
    }

    // normalize
    // Takes a validated item and returns a clean copy with only expected fields.
    // This keeps old data tidy and makes sure types are consistent.
    function normalize(x) {
        var out = {
            id: x.id,
            title: String(x.title),
            price: String(x.price),
            imagePath: String(x.imagePath),
            userId: String(x.userId),
            userName: String(x.userName),
            viewedAt: Number(x.viewedAt)
        };

        if (x.categoryId != null) {
            out.categoryId = Number(x.categoryId);
        }

        if (typeof x.category === "string") {
            out.category = String(x.category);
        }

        if (typeof x.location === "string") {
            out.location = String(x.location);
        }

        if (x.priceValue != null) {
            out.priceValue = Number(x.priceValue);
        }

        return out;
    }

    // list
    // Returns the current history as a clean array.
    // It removes duplicates, bad items, and optionally hides your own ads.
    // currentUserId is optional. If provided, ads that belong to you are excluded.
    function list(currentUserId) {
        var raw = readRaw();

        if (!raw) {
            return [];
        }

        var seen = Object.create(null);
        var out = [];

        for (var i = 0; i < raw.length; i++) {
            var item = raw[i];

            if (!isValidItem(item)) {
                continue;
            }

            if (seen[item.id]) {
                continue;
            }

            if (currentUserId && item.userId === currentUserId) {
                continue;
            }

            seen[item.id] = true;

            out.push(normalize(item));

            if (out.length >= MAX_ITEMS) {
                break;
            }
        }

        return out;
    }

    // record
    // Adds an ad to the front of the history. If the ad was already there
    // it moves to the front. Keeps the list at most MAX_ITEMS long.
    // ad shape: { id, title, price, imagePath, userId, userName, categoryId, ... }
    function record(ad) {
        if (!ad || typeof ad !== "object") {
            return;
        }

        if (typeof ad.id !== "number" || !isFinite(ad.id) || ad.id <= 0) {
            return;
        }

        var item = {
            id: ad.id,
            title: typeof ad.title === "string" ? ad.title : "",
            price: typeof ad.price === "string" ? ad.price : "",
            imagePath: typeof ad.imagePath === "string" ? ad.imagePath : "",
            userId: typeof ad.userId === "string" ? ad.userId : "",
            userName: typeof ad.userName === "string" ? ad.userName : "",
            viewedAt: Date.now()
        };

        if (typeof ad.categoryId === "number" && isFinite(ad.categoryId) && ad.categoryId > 0) {
            item.categoryId = ad.categoryId;
        }

        if (typeof ad.category === "string") {
            item.category = ad.category;
        }

        if (typeof ad.location === "string") {
            item.location = ad.location;
        }

        if (typeof ad.priceValue === "number" && isFinite(ad.priceValue)) {
            item.priceValue = ad.priceValue;
        }

        // Older payloads may send categoryId as a string, so we try to parse it.
        if (item.categoryId == null && ad.categoryId != null) {
            var parsed = parseInt(ad.categoryId, 10);

            if (!isNaN(parsed) && parsed > 0) {
                item.categoryId = parsed;
            }
        }

        var existing = readRaw() || [];
        var next = [item];

        for (var i = 0; i < existing.length; i++) {
            var cur = existing[i];

            if (!isValidItem(cur)) {
                continue;
            }

            if (cur.id === item.id) {
                continue;
            }

            next.push(cur);

            if (next.length >= MAX_ITEMS) {
                break;
            }
        }

        writeRaw(next);
    }

    // clear
    // Removes all saved history.
    function clear() {
        if (!hasStorage) {
            return;
        }

        try {
            window.localStorage.removeItem(STORAGE_KEY);
        } catch (_) {}
    }

    // validateAndPrune
    // Asks the server which ids still exist and removes deleted ads from storage.
    // We run this in the background after rendering so the page does not wait.
    // If the network fails we just keep the old data and try again next time.
    async function validateAndPrune(currentUserId) {
        var items = list(currentUserId || null);

        if (items.length === 0) {
            return;
        }

        var ids = items.map(function (x) {
            return x.id;
        });

        try {
            var res = await fetch("/Home/ValidateRecent?ids=" + encodeURIComponent(ids.join(",")), {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            if (!res.ok) {
                return;
            }

            var data = await res.json();

            var existing = data && Array.isArray(data.existingIds) ? data.existingIds : data.existingIds || [];

            var existingSet = Object.create(null);

            for (var i = 0; i < existing.length; i++) {
                existingSet[existing[i]] = true;
            }

            var raw = readRaw() || [];
            var filtered = [];
            var changed = false;

            for (var j = 0; j < raw.length; j++) {
                var cur = raw[j];

                if (!isValidItem(cur)) {
                    changed = true;
                    continue;
                }

                if (!existingSet[cur.id]) {
                    changed = true;
                    continue;
                }

                filtered.push(cur);
            }

            if (changed) {
                writeRaw(filtered);
            }
        } catch (_) {}
    }

    // render
    // Paints the Recently Browsed strip into a container.
    // It hides the container when there is nothing to show.
    // Uses HorizontalScroller if it is loaded, otherwise falls back.
    function render(container) {
        if (!container) {
            return;
        }

        var currentUserId = container.getAttribute("data-current-user-id") || "";
        var items = list(currentUserId || null);

        // Use the shared helper when available. This keeps both strips in sync.
        if (window.HorizontalScroller) {
            var hs = window.HorizontalScroller;

            hs.createStrip(container, items, {
                title: "Recently browsed",
                icon: "\uD83D\uDD58",
                showClear: true,
                showNav: true,
                emptyHidden: true,
                onClear: function () {
                    clear();
                    render(container);

                    // Recommendations depend on this history, so refresh them too.
                    if (window.Recommended) {
                        var recEl = document.getElementById("recommendedContainer");

                        if (recEl) {
                            window.Recommended.render(recEl);
                        }
                    }

                    try {
                        window.dispatchEvent(new Event("recentlyViewed:cleared"));
                    } catch (_) {}
                }
            });

            // Clean up deleted ads without blocking the first paint.
            if (items.length > 0) {
                validateAndPrune(currentUserId).then(function () {
                    var fresh = list(currentUserId || null);

                    if (fresh.length !== items.length) {
                        hs.createStrip(container, fresh, {
                            title: "Recently browsed",
                            icon: "\uD83D\uDD58",
                            showClear: true,
                            showNav: true,
                            emptyHidden: true,
                            onClear: function () {
                                clear();
                                render(container);
                            }
                        });
                    }
                });
            }

            return;
        }

        // Simple fallback if HorizontalScroller did not load.
        container.innerHTML = "";

        if (items.length === 0) {
            container.hidden = true;
            return;
        }

        container.hidden = false;
    }

    // Keep other tabs in sync when storage changes in another tab.
    window.addEventListener("storage", function (e) {
        if (e.key !== STORAGE_KEY) {
            return;
        }

        var el = document.getElementById("recentlyBrowsedContainer");

        if (el) {
            render(el);
        }

        var recEl = document.getElementById("recommendedContainer");

        if (recEl && window.Recommended) {
            window.Recommended.render(recEl);
        }
    });

    window.RecentlyViewed = {
        record: record,
        render: render,
        clear: clear,
        list: list,
        validateAndPrune: validateAndPrune,
        STORAGE_KEY: STORAGE_KEY,
        MAX_ITEMS: MAX_ITEMS
    };
})();
