// HorizontalScroller - shared helper for the home page strips
// This file powers both the Recently Browsed and the Recommended bars.
// It keeps the look and behavior the same so we do not duplicate code.
// No external libraries are needed, just plain JavaScript.

(function () {
    "use strict";

    var FALLBACK_IMG = "/plusSign.png";

    // escapeAttr
    // Makes a string safe to use inside an HTML attribute.
    // We replace the characters that could break the markup.
    function escapeAttr(s) {
        return String(s)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    // buildCard
    // Creates one ad card for the horizontal track.
    // Input is an object like { id, title, price, imagePath, userId, userName, categoryId, ... }
    // Returns an anchor element that links to the ad details page.
    // We also store a small JSON payload on the card so clicks can be
    // saved to the Recently Viewed history without another request.
    function buildCard(item) {
        var link = document.createElement("a");
        link.href = "/Advertisement/Show/" + item.id;
        link.className = "hscroll-card recent-card text-decoration-none";

        try {
            var payload = {
                id: item.id,
                title: item.title || "",
                price: item.price || "",
                imagePath: item.imagePath || "",
                userId: item.userId || "",
                userName: item.userName || ""
            };

            // Keep extra fields if we have them. They help the recommendation
            // logic and do not hurt older code that ignores them.
            if (item.categoryId != null) {
                payload.categoryId = item.categoryId;
            }

            if (item.category) {
                payload.category = item.category;
            }

            if (item.location) {
                payload.location = item.location;
            }

            if (item.priceValue != null) {
                payload.priceValue = item.priceValue;
            }

            link.setAttribute("data-recent-ad", JSON.stringify(payload));
        } catch (_) {}

        var img = document.createElement("img");
        img.className = "hscroll-card-img recent-card-img";
        img.loading = "lazy";
        img.alt = escapeAttr(item.title || "Listing");
        img.src = item.imagePath || FALLBACK_IMG;

        img.onerror = function () {
            this.onerror = null;
            this.src = FALLBACK_IMG;
        };

        var title = document.createElement("div");
        title.className = "hscroll-card-title recent-card-title text-truncate";
        title.textContent = item.title || "(untitled)";

        var price = document.createElement("div");
        price.className = "hscroll-card-price recent-card-price";
        price.textContent = item.price || "";

        link.appendChild(img);
        link.appendChild(title);
        link.appendChild(price);

        return link;
    }

    // scrollBy
    // Moves the track left or right when the user clicks the arrow buttons.
    // direction is -1 for left and 1 for right.
    function scrollBy(container, direction) {
        var track = container.querySelector(".hscroll-track, .recent-track");

        if (!track) {
            return;
        }

        var amount = Math.max(200, Math.floor(track.clientWidth * 0.8));

        track.scrollBy({ left: amount * direction, behavior: "smooth" });
    }

    // buildHeader
    // Creates the header row above the track. It shows the title,
    // an optional icon, a Clear button, and the left/right scroll buttons.
    // opts: { title, icon, showClear, onClear, showNav }
    // container is needed so the nav buttons can call scrollBy.
    function buildHeader(opts, container) {
        var header = document.createElement("div");
        header.className = "hscroll-header recent-header d-flex align-items-center mb-2";

        var title = document.createElement("div");
        title.className = "hscroll-header-title recent-header-title fw-semibold";
        title.textContent = opts.title || "";

        if (opts.icon) {
            var icon = document.createElement("span");
            icon.setAttribute("aria-hidden", "true");
            icon.className = "me-2";
            icon.textContent = opts.icon;
            title.insertBefore(icon, title.firstChild);
        }

        var spacer = document.createElement("div");
        spacer.className = "flex-grow-1";

        header.appendChild(title);
        header.appendChild(spacer);

        if (opts.showClear) {
            var clearBtn = document.createElement("button");
            clearBtn.type = "button";
            clearBtn.className = "btn btn-link btn-sm hscroll-clear recent-clear";
            clearBtn.textContent = "Clear";

            clearBtn.addEventListener("click", function () {
                if (typeof opts.onClear === "function") {
                    opts.onClear();
                }
            });

            header.appendChild(clearBtn);
        }

        if (opts.showNav) {
            var prevBtn = document.createElement("button");
            prevBtn.type = "button";
            prevBtn.className = "btn btn-sm btn-outline-secondary hscroll-nav recent-nav d-none d-md-inline-flex align-items-center justify-content-center";
            prevBtn.setAttribute("aria-label", "Scroll left");
            prevBtn.innerHTML = "&#8249;";

            prevBtn.addEventListener("click", function () {
                scrollBy(container, -1);
            });

            var nextBtn = document.createElement("button");
            nextBtn.type = "button";
            nextBtn.className = "btn btn-sm btn-outline-secondary hscroll-nav recent-nav d-none d-md-inline-flex align-items-center justify-content-center";
            nextBtn.setAttribute("aria-label", "Scroll right");
            nextBtn.innerHTML = "&#8250;";

            nextBtn.addEventListener("click", function () {
                scrollBy(container, 1);
            });

            var nav = document.createElement("div");
            nav.className = "d-flex gap-1";
            nav.appendChild(prevBtn);
            nav.appendChild(nextBtn);

            header.appendChild(nav);
        }

        return header;
    }

    // createStrip
    // This is the main helper that fills a container with a full strip.
    // It clears the container, adds a header, then adds a track with cards.
    // If items is empty and emptyHidden is true, the container stays hidden.
    // container: the outer div for the strip
    // items: array of card data
    // opts: { title, icon, showClear, onClear, showNav, emptyHidden, emptyText }
    function createStrip(container, items, opts) {
        if (!container) {
            return;
        }

        opts = opts || {};

        var emptyHidden = opts.emptyHidden !== false;

        container.innerHTML = "";

        if (!items || items.length === 0) {
            if (emptyHidden) {
                container.hidden = true;
            } else {
                container.hidden = false;
            }

            if (!emptyHidden) {
                var empty = document.createElement("div");
                empty.className = "text-muted small py-2";
                empty.textContent = opts.emptyText || "No items to show.";
                container.appendChild(empty);
            }

            return;
        }

        container.hidden = false;

        var header = buildHeader({
            title: opts.title,
            icon: opts.icon,
            showClear: !!opts.showClear,
            onClear: opts.onClear,
            showNav: opts.showNav !== false
        }, container);

        var track = document.createElement("div");
        track.className = "hscroll-track recent-track";

        for (var i = 0; i < items.length; i++) {
            track.appendChild(buildCard(items[i]));
        }

        container.appendChild(header);
        container.appendChild(track);
    }

    window.HorizontalScroller = {
        FALLBACK_IMG: FALLBACK_IMG,
        escapeAttr: escapeAttr,
        buildCard: buildCard,
        buildHeader: buildHeader,
        createStrip: createStrip,
        scrollBy: scrollBy
    };
})();
