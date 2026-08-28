// RecommendedAds - shows suggestions based on what you recently viewed.
// It reads your Recently Browsed history, asks the server for similar ads,
// and renders them in the same horizontal strip style.
// If you have no history or nothing similar exists, the bar stays hidden.
// Limits are capped so we do not show too many cards at once.

(function () {
    "use strict";

    // REC_CFG
    // Central place for tuning the recommendation behavior.
    // Change these numbers if you want more or fewer cards, or different limits.
    var REC_CFG = {
        MAX_RECOMMENDED: 15,
        FETCH_LIMIT: 20,
        STORAGE_KEY_REC: "mb.recommendedCache",
        DEBOUNCE_MS: 150
    };

    var inflight = null;
    var lastRenderedIds = "";

    // currentIds
    // Helper to pull just the ids out of a history list.
    function currentIds(list) {
        return list.map(function (x) {
            return x.id;
        });
    }

    // buildPayload
    // Builds the JSON body we send to the server.
    // We only send ids and a limit. The server does the smart scoring.
    function buildPayload(ids) {
        return {
            viewedIds: ids,
            limit: REC_CFG.FETCH_LIMIT
        };
    }

    // fetchRecommendations
    // Calls the server and returns a list of ad objects.
    // Handles aborting a previous request, empty input, and bad responses.
    // Returns [] on failure and null if the request was aborted.
    async function fetchRecommendations(ids) {
        if (!ids || ids.length === 0) {
            return [];
        }

        if (inflight) {
            try {
                inflight.abort();
            } catch (_) {}
        }

        var ctrl = new AbortController();
        inflight = ctrl;

        try {
            var res = await fetch("/Home/Recommendations", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify(buildPayload(ids)),
                signal: ctrl.signal
            });

            if (!res.ok) {
                throw new Error("recs fetch " + res.status);
            }

            var data = await res.json();
            var ads = (data && Array.isArray(data.ads)) ? data.ads : [];

            // Filter out anything that does not look like a real ad.
            var out = [];

            for (var i = 0; i < ads.length; i++) {
                var a = ads[i];

                if (!a || typeof a.id !== "number" || !isFinite(a.id) || a.id <= 0) {
                    continue;
                }

                if (typeof a.title !== "string") {
                    continue;
                }

                out.push(a);

                if (out.length >= REC_CFG.MAX_RECOMMENDED) {
                    break;
                }
            }

            return out;
        } catch (e) {
            if (e && e.name === "AbortError") {
                return null;
            }

            console.warn("[Recommended] fetch failed", e);

            return [];
        } finally {
            if (inflight === ctrl) {
                inflight = null;
            }
        }
    }

    // render
    // Main entry point. Fills the container with recommended ads.
    // Steps: read history, fetch from server, map to card shape, render.
    // Hides the container if there is no history or no results.
    async function render(container) {
        if (!container) {
            return;
        }

        if (!window.HorizontalScroller || !window.RecentlyViewed) {
            container.hidden = true;
            return;
        }

        var currentUserId = container.getAttribute("data-current-user-id") || "";
        var history = window.RecentlyViewed.list(currentUserId || null);

        if (history.length === 0) {
            container.hidden = true;
            container.innerHTML = "";
            lastRenderedIds = "";
            return;
        }

        var ids = currentIds(history);
        var key = ids.join(",") + "|" + currentUserId;

        // Skip if we already rendered the same list and the DOM is still there.
        if (key === lastRenderedIds && !container.hidden && container.querySelector(".hscroll-track")) {
            return;
        }

        var ads = await fetchRecommendations(ids);

        if (ads === null) {
            return;
        }

        if (!ads || ads.length === 0) {
            // Nothing to recommend right now, maybe ads were deleted or no similar ones.
            container.hidden = true;
            container.innerHTML = "";
            lastRenderedIds = key;
            return;
        }

        // Convert server data to the shape HorizontalScroller expects.
        var items = ads.map(function (a) {
            return {
                id: a.id,
                title: a.title,
                price: a.price || "",
                priceValue: a.priceValue,
                imagePath: a.imagePath,
                userId: a.userId || "",
                userName: a.userName || "",
                categoryId: a.categoryId,
                category: a.category,
                location: a.location
            };
        });

        if (items.length > REC_CFG.MAX_RECOMMENDED) {
            items = items.slice(0, REC_CFG.MAX_RECOMMENDED);
        }

        window.HorizontalScroller.createStrip(container, items, {
            title: "Recommended for you",
            icon: "\u2728",
            showClear: false,
            showNav: true,
            emptyHidden: true
        });

        lastRenderedIds = key;
    }

    // attachRecHandlers
    // Listens for clicks inside the recommended strip and saves those ads
    // to Recently Viewed as well, so the history keeps growing.
    function attachRecHandlers() {
        var el = document.getElementById("recommendedContainer");

        if (!el) {
            return;
        }

        el.addEventListener("click", function (e) {
            var card = e.target.closest("a[data-recent-ad]");

            if (!card) {
                return;
            }

            if (!window.RecentlyViewed) {
                return;
            }

            var raw = card.getAttribute("data-recent-ad");

            if (!raw) {
                return;
            }

            try {
                window.RecentlyViewed.record(JSON.parse(raw));
            } catch (_) {}
        });
    }

    // Re-render when history changes in another tab.
    window.addEventListener("storage", function (e) {
        if (!window.RecentlyViewed) {
            return;
        }

        if (e.key !== window.RecentlyViewed.STORAGE_KEY) {
            return;
        }

        var el = document.getElementById("recommendedContainer");

        if (el) {
            render(el);
        }
    });

    // Also re-render when the user clears history in the same tab.
    window.addEventListener("recentlyViewed:cleared", function () {
        var el = document.getElementById("recommendedContainer");

        if (el) {
            render(el);
        }
    });

    window.Recommended = {
        render: render,
        attachHandlers: attachRecHandlers,
        CFG: REC_CFG
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", attachRecHandlers);
    } else {
        attachRecHandlers();
    }
})();
