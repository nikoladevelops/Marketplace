/* ============================================================
   Marketplace UI script
   This file handles the theme switch, the bootstrap modals,
   and the fullscreen lightbox for images.
   Keep it simple and keep it fast.
   ============================================================ */


/* ---------- Theme handling, dark is the default ---------- */

// currentTheme
// Checks what theme is currently set on the html tag.
// Returns "light" or "dark". Defaults to dark if nothing is set.
function currentTheme() {
    if (document.documentElement.getAttribute("data-theme") === "light") {
        return "light";
    }

    return "dark";
}

// toggleTheme
// Flips between light and dark, saves the choice, and tells
// the rest of the page that the theme changed.
function toggleTheme() {
    var next = currentTheme() === "dark" ? "light" : "dark";

    document.documentElement.setAttribute("data-theme", next);
    localStorage.setItem("theme", next);

    updateThemeToggleIcon();

    document.dispatchEvent(new CustomEvent("themechange"));
}

// updateThemeToggleIcon
// Updates the little moon/sun button in the navbar so it matches
// the current theme. Does nothing if the button is not on the page.
function updateThemeToggleIcon() {
    var btn = document.getElementById("themeToggle");

    if (!btn) {
        return;
    }

    var dark = currentTheme() === "dark";
    var icon = document.getElementById("themeToggleIcon");
    var text = document.getElementById("themeToggleText");

    if (icon) {
        icon.textContent = dark ? "\uD83C\uDF19" : "\u2600\uFE0F";
    } else {
        btn.textContent = dark ? "\uD83C\uDF19" : "\u2600\uFE0F";
    }

    if (text) {
        text.textContent = dark ? "Dark mode" : "Light mode";
    }

    if (dark) {
        btn.title = "Switch to light mode";
    } else {
        btn.title = "Switch to dark mode";
    }
}

document.addEventListener("DOMContentLoaded", updateThemeToggleIcon);


/* ---------- Tiny helpers for Bootstrap modals ---------- */

// showModal
// Opens a bootstrap modal by id. Safe to call even if bootstrap is not loaded.
function showModal(id) {
    var el = document.getElementById(id);

    if (el && window.bootstrap) {
        bootstrap.Modal.getOrCreateInstance(el).show();
    }
}

// hideModal
// Closes a bootstrap modal by id.
function hideModal(id) {
    var el = document.getElementById(id);

    if (el && window.bootstrap) {
        bootstrap.Modal.getOrCreateInstance(el).hide();
    }
}

// closeModal
// Old name that some views still use. Just calls hideModal.
function closeModal(id) {
    hideModal(id);
}


/* ============================================================
   Lightbox - fullscreen image viewer
   Supports wheel zoom, pinch zoom, dragging, toolbar buttons,
   and grouping images with data-lightbox="groupName".
   ============================================================ */
var Lightbox = (function () {
    var overlay = null;
    var stage = null;
    var wrap = null;
    var imgEl = null;
    var counterEl = null;
    var zoomLabel = null;
    var prevBtn = null;
    var nextBtn = null;

    var MIN_SCALE = 0.5;
    var MAX_SCALE = 6;

    var scale = 1;
    var offsetX = 0;
    var offsetY = 0;

    var items = [];
    var currentIndex = 0;

    var dragging = false;
    var lastX = 0;
    var lastY = 0;
    var dragMoved = 0;
    var suppressClickUntil = 0;

    // clamp
    // Keeps a number inside a min/max range.
    function clamp(v, lo, hi) {
        return Math.min(hi, Math.max(lo, v));
    }

    // applyTransform
    // Applies the current scale and offset to the image wrapper.
    function applyTransform() {
        wrap.style.transform = "translate(" + offsetX + "px, " + offsetY + "px) scale(" + scale + ")";

        if (scale > 1) {
            wrap.classList.add("pannable");
        } else {
            wrap.classList.remove("pannable");
        }
    }

    // updateZoomLabel
    // Shows the zoom percent in the bottom toolbar.
    function updateZoomLabel() {
        zoomLabel.textContent = Math.round(scale * 100) + "%";
    }

    // setZoom
    // Zooms to a new scale. Keeps the point under the cursor in place.
    // If no anchor is given, we zoom towards the center of the stage.
    function setZoom(nextScale, anchorX, anchorY) {
        var rect = stage.getBoundingClientRect();

        if (anchorX === undefined) {
            anchorX = rect.left + rect.width / 2;
            anchorY = rect.top + rect.height / 2;
        }

        nextScale = clamp(nextScale, MIN_SCALE, MAX_SCALE);

        var factor = nextScale / scale;

        var cx = anchorX - (rect.left + rect.width / 2);
        var cy = anchorY - (rect.top + rect.height / 2);

        offsetX = cx - factor * (cx - offsetX);
        offsetY = cy - factor * (cy - offsetY);

        scale = nextScale;

        if (scale <= 1.001) {
            scale = 1;
            offsetX = 0;
            offsetY = 0;
        } else {
            clampOffsets();
        }

        applyTransform();
        updateZoomLabel();
    }

    // resetView
    // Brings the image back to normal size and centers it.
    function resetView() {
        wrap.style.transition = "none";

        scale = 1;
        offsetX = 0;
        offsetY = 0;

        applyTransform();
        updateZoomLabel();

        requestAnimationFrame(function () {
            wrap.style.transition = "";
        });
    }

    // clampOffsets
    // Stops the image from being dragged too far out of view when zoomed.
    function clampOffsets() {
        if (scale <= 1) {
            offsetX = 0;
            offsetY = 0;
            return;
        }

        var stageW = stage.clientWidth;
        var stageH = stage.clientHeight;

        var rect = imgEl.getBoundingClientRect();

        var renderedW = rect.width / scale;
        var renderedH = rect.height / scale;

        var scaledW = renderedW * scale;
        var scaledH = renderedH * scale;

        var maxX = Math.max(0, (scaledW - stageW) / 2 + 40);
        var maxY = Math.max(0, (scaledH - stageH) / 2 + 40);

        offsetX = Math.max(-maxX, Math.min(maxX, offsetX));
        offsetY = Math.max(-maxY, Math.min(maxY, offsetY));
    }

    // render
    // Shows the current image from the items list.
    function render() {
        var item = items[currentIndex];

        if (!item) {
            return;
        }

        wrap.style.transition = "none";

        resetView();

        imgEl.src = item.src;
        imgEl.alt = item.alt || "";

        if (items.length > 1) {
            counterEl.textContent = (currentIndex + 1) + " / " + items.length;
        } else {
            counterEl.textContent = "";
        }

        if (items.length < 2) {
            prevBtn.classList.add("lb-hidden");
            nextBtn.classList.add("lb-hidden");
        } else {
            prevBtn.classList.remove("lb-hidden");
            nextBtn.classList.remove("lb-hidden");
        }

        preloadNeighbors();

        dragMoved = 0;

        requestAnimationFrame(function () {
            wrap.style.transition = "";
        });
    }

    // preloadNeighbors
    // Loads the next and previous images in the background so
    // switching feels instant.
    function preloadNeighbors() {
        if (items.length < 2) {
            return;
        }

        [currentIndex - 1, currentIndex + 1].forEach(function (i) {
            var j = (i + items.length) % items.length;

            if (items[j]) {
                var im = new Image();
                im.src = items[j].src;
            }
        });
    }

    // go
    // Moves to the next or previous image.
    function go(delta) {
        if (items.length < 2) {
            return;
        }

        currentIndex = (currentIndex + delta + items.length) % items.length;

        render();
    }

    // open
    // Opens the lightbox with a list of images and a starting index.
    function open(list, index) {
        items = list || [];
        currentIndex = clamp(index || 0, 0, Math.max(0, items.length - 1));

        if (!items.length) {
            return;
        }

        buildDomOnce();
        render();

        overlay.classList.add("open");
        document.body.style.overflow = "hidden";
    }

    // close
    // Closes the lightbox and restores page scrolling.
    function close() {
        overlay.classList.remove("open");
        document.body.style.overflow = "";
    }

    // buildDomOnce
    // Creates the lightbox DOM the first time it is needed.
    function buildDomOnce() {
        if (overlay) {
            return;
        }

        overlay = document.createElement("div");
        overlay.className = "lb-overlay";

        overlay.innerHTML =
            '<div class="lb-topbar">' +
                '<span class="lb-counter"></span>' +
                '<button type="button" class="lb-btn lb-close" title="Close (Esc)">\u2715</button>' +
            '</div>' +
            '<div class="lb-stage">' +
                '<div class="lb-img-wrap"><img alt=""></div>' +
            '</div>' +
            '<button type="button" class="lb-nav lb-prev" title="Previous (\u2190)">\u2039</button>' +
            '<button type="button" class="lb-nav lb-next" title="Next (\u2192)">\u203A</button>' +
            '<div class="lb-toolbar">' +
                '<button type="button" class="lb-btn lb-zoom-out" title="Zoom out (-)">\u2212</button>' +
                '<span class="lb-zoom-label">100%</span>' +
                '<button type="button" class="lb-btn lb-zoom-in" title="Zoom in (+)">+</button>' +
                '<button type="button" class="lb-btn lb-reset" title="Reset view (0 or double-click)">\u27F2</button>' +
            '</div>';

        counterEl = overlay.querySelector(".lb-counter");
        prevBtn = overlay.querySelector(".lb-prev");
        nextBtn = overlay.querySelector(".lb-next");
        zoomLabel = overlay.querySelector(".lb-zoom-label");
        stage = overlay.querySelector(".lb-stage");
        wrap = overlay.querySelector(".lb-img-wrap");
        imgEl = overlay.querySelector("img");

        imgEl.draggable = false;

        imgEl.addEventListener("dragstart", function (e) {
            e.preventDefault();
        });

        bindEvents();

        document.body.appendChild(overlay);
    }

    // bindEvents
    // Hooks up all the buttons, keyboard, mouse and touch handlers.
    function bindEvents() {
        overlay.querySelector(".lb-close").addEventListener("click", function (e) {
            e.stopPropagation();
            close();
        });

        prevBtn.addEventListener("click", function (e) {
            e.stopPropagation();
            e.preventDefault();
            go(-1);
        });

        nextBtn.addEventListener("click", function (e) {
            e.stopPropagation();
            e.preventDefault();
            go(1);
        });

        overlay.querySelector(".lb-zoom-in").addEventListener("click", function (e) {
            e.stopPropagation();
            setZoom(scale * 1.25);
        });

        overlay.querySelector(".lb-zoom-out").addEventListener("click", function (e) {
            e.stopPropagation();
            setZoom(scale / 1.25);
        });

        overlay.querySelector(".lb-reset").addEventListener("click", function (e) {
            e.stopPropagation();
            resetView();
        });

        overlay.querySelector(".lb-topbar").addEventListener("click", function (e) {
            e.stopPropagation();
        });

        overlay.querySelector(".lb-toolbar").addEventListener("click", function (e) {
            e.stopPropagation();
        });

        // Wheel zoom - zoom towards the mouse pointer.
        stage.addEventListener("wheel", function (e) {
            e.preventDefault();

            var factor = e.deltaY < 0 ? 1.12 : 1 / 1.12;

            setZoom(scale * factor, e.clientX, e.clientY);
        }, { passive: false });

        // Double click toggles between fit and 2x zoom.
        function handleDblClick(e) {
            e.preventDefault();
            e.stopPropagation();

            suppressClickUntil = Date.now() + 400;
            dragMoved = 999;

            if (scale > 1.01) {
                resetView();
            } else {
                setZoom(2, e.clientX, e.clientY);
            }
        }

        stage.addEventListener("dblclick", handleDblClick);
        wrap.addEventListener("dblclick", handleDblClick);

        wrap.addEventListener("dragstart", function (e) {
            e.preventDefault();
        });

        stage.addEventListener("dragstart", function (e) {
            e.preventDefault();
        });

        // Drag to pan when zoomed in.
        stage.addEventListener("pointerdown", function (e) {
            if (e.target.closest && e.target.closest(".lb-nav")) {
                return;
            }

            if (e.button !== 0 || scale <= 1) {
                return;
            }

            e.preventDefault();

            dragging = true;
            dragMoved = 0;
            lastX = e.clientX;
            lastY = e.clientY;

            wrap.style.transition = "none";
            wrap.classList.add("panning");

            try {
                stage.setPointerCapture(e.pointerId);
            } catch (err) {}
        });

        stage.addEventListener("pointermove", function (e) {
            if (!dragging) {
                return;
            }

            e.preventDefault();

            var dx = e.clientX - lastX;
            var dy = e.clientY - lastY;

            offsetX += dx;
            offsetY += dy;

            clampOffsets();

            dragMoved += Math.abs(dx) + Math.abs(dy);
            lastX = e.clientX;
            lastY = e.clientY;

            applyTransform();
        });

        ["pointerup", "pointercancel", "pointerleave"].forEach(function (type) {
            stage.addEventListener(type, function (e) {
                if (!dragging) {
                    return;
                }

                dragging = false;
                wrap.classList.remove("panning");
                wrap.style.transition = "";

                try {
                    stage.releasePointerCapture(e.pointerId);
                } catch (err) {}
            });
        });

        // Pinch zoom for touch devices.
        var pinchStartDist = 0;
        var pinchStartScale = 1;

        function touchDistance(touches) {
            var dx = touches[0].clientX - touches[1].clientX;
            var dy = touches[0].clientY - touches[1].clientY;

            return Math.hypot(dx, dy);
        }

        stage.addEventListener("touchstart", function (e) {
            if (e.touches.length === 2) {
                pinchStartDist = touchDistance(e.touches);
                pinchStartScale = scale;
            }
        }, { passive: true });

        stage.addEventListener("touchmove", function (e) {
            if (e.touches.length === 2 && pinchStartDist > 0) {
                e.preventDefault();

                var d = touchDistance(e.touches);
                setZoom(pinchStartScale * (d / pinchStartDist));
            }
        }, { passive: false });

        stage.addEventListener("touchend", function () {
            pinchStartDist = 0;
        });

        // Click on the dark background closes, but we ignore the click
        // that comes right after a drag or a double click.
        stage.addEventListener("click", function (e) {
            if (Date.now() < suppressClickUntil) {
                return;
            }

            if (e.detail > 1) {
                return;
            }

            var moved = dragMoved;
            dragMoved = 0;

            if (e.target !== stage || moved > 5) {
                return;
            }

            close();
        });

        // Keyboard shortcuts when the lightbox is open.
        document.addEventListener("keydown", function (e) {
            if (!overlay.classList.contains("open")) {
                return;
            }

            if (e.key === "Escape") {
                close();
            } else if (e.key === "ArrowLeft") {
                go(-1);
            } else if (e.key === "ArrowRight") {
                go(1);
            } else if (e.key === "+" || e.key === "=") {
                setZoom(scale * 1.25);
            } else if (e.key === "-") {
                setZoom(scale / 1.25);
            } else if (e.key === "0") {
                resetView();
            }
        });
    }

    return {
        open: open,
        openSingle: function (src, alt) {
            open([{ src: src, alt: alt || "" }], 0);
        },
        close: close
    };
})();

// openLightbox
// Simple helper that views call directly. Opens a single image.
function openLightbox(src, alt) {
    Lightbox.openSingle(src, alt);
}

// Gallery helper
// Any image with data-lightbox="groupName" will open together
// with the rest of the images that share the same group.
document.addEventListener("click", function (e) {
    var img = e.target.closest ? e.target.closest("img[data-lightbox]") : null;

    if (!img) {
        return;
    }

    var group = img.getAttribute("data-lightbox");

    var groupImgs = Array.prototype.slice.call(
        document.querySelectorAll('img[data-lightbox="' + group + '"]')
    );

    var list = groupImgs.map(function (el) {
        return { src: el.currentSrc || el.src, alt: el.alt };
    });

    Lightbox.open(list, groupImgs.indexOf(img));
});
