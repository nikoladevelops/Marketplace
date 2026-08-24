/* ============================================================
   Marketplace UI script: theme toggle, modal helpers, lightbox
   ============================================================ */

/* ---------- Theme (dark is default) ---------- */
function currentTheme() {
    return document.documentElement.getAttribute("data-theme") === "light" ? "light" : "dark";
}

function toggleTheme() {
    var next = currentTheme() === "dark" ? "light" : "dark";
    document.documentElement.setAttribute("data-theme", next);
    localStorage.setItem("theme", next);
    updateThemeToggleIcon();
    document.dispatchEvent(new CustomEvent("themechange"));
}

function updateThemeToggleIcon() {
    var btn = document.getElementById("themeToggle");
    if (!btn) return;
    var dark = currentTheme() === "dark";
    btn.textContent = dark ? "\uD83C\uDF19" : "\u2600\uFE0F"; // moon / sun
    btn.title = dark ? "Switch to light mode" : "Switch to dark mode";
}

document.addEventListener("DOMContentLoaded", updateThemeToggleIcon);

/* ---------- Bootstrap modal helpers ---------- */
function showModal(id) {
    var el = document.getElementById(id);
    if (el && window.bootstrap) {
        bootstrap.Modal.getOrCreateInstance(el).show();
    }
}

function hideModal(id) {
    var el = document.getElementById(id);
    if (el && window.bootstrap) {
        bootstrap.Modal.getOrCreateInstance(el).hide();
    }
}

/* Alias kept because views call closeModal() directly */
function closeModal(id) {
    hideModal(id);
}

/* ============================================================
   Lightbox — fullscreen viewer: wheel/pinch zoom, drag pan,
   toolbar controls, gallery navigation via data-lightbox groups
   ============================================================ */
var Lightbox = (function () {
    var overlay = null;
    var stage, wrap, imgEl, counterEl, zoomLabel, prevBtn, nextBtn;

    var MIN_SCALE = 0.5;
    var MAX_SCALE = 6;
    var scale = 1;
    var offsetX = 0;
    var offsetY = 0;

    var items = [];
    var currentIndex = 0;

    function clamp(v, lo, hi) {
        return Math.min(hi, Math.max(lo, v));
    }

    function applyTransform(instant) {
        if (instant) {
            wrap.style.transition = "none";
            void wrap.offsetWidth; // flush so the next change animates again
        } else {
            wrap.style.transition = "";
        }
        wrap.style.transform = "translate(" + offsetX + "px, " + offsetY + "px) scale(" + scale + ")";
        stage.classList.toggle("pannable", scale > 1);
    }

    function updateZoomLabel() {
        zoomLabel.textContent = Math.round(scale * 100) + "%";
    }

    /* Zoom to `nextScale`, keeping the point under (anchorX, anchorY)
       visually stationary. Anchor defaults to viewport center. */
    function setZoom(nextScale, anchorX, anchorY) {
        var rect = stage.getBoundingClientRect();
        if (anchorX === undefined) {
            anchorX = rect.left + rect.width / 2;
            anchorY = rect.top + rect.height / 2;
        }

        nextScale = clamp(nextScale, MIN_SCALE, MAX_SCALE);
        var factor = nextScale / scale;

        // Cursor position relative to stage center
        var cx = anchorX - (rect.left + rect.width / 2);
        var cy = anchorY - (rect.top + rect.height / 2);

        offsetX = cx - factor * (cx - offsetX);
        offsetY = cy - factor * (cy - offsetY);

        scale = nextScale;
        if (scale <= 1.001) {
            scale = 1;
            offsetX = 0;
            offsetY = 0;
        }

        applyTransform();
        updateZoomLabel();
    }

    function resetView(instant) {
        scale = 1;
        offsetX = 0;
        offsetY = 0;
        applyTransform(instant);
        updateZoomLabel();
    }

    function render() {
        var item = items[currentIndex];
        if (!item) return;
        resetView(true);
        imgEl.src = item.src;
        imgEl.alt = item.alt || "";
        counterEl.textContent = items.length > 1
            ? (currentIndex + 1) + " / " + items.length
            : "";
        prevBtn.classList.toggle("lb-hidden", items.length < 2);
        nextBtn.classList.toggle("lb-hidden", items.length < 2);
        preloadNeighbors();
    }

    function preloadNeighbors() {
        if (items.length < 2) return;
        [currentIndex - 1, currentIndex + 1].forEach(function (i) {
            var j = (i + items.length) % items.length;
            if (items[j]) {
                var im = new Image();
                im.src = items[j].src;
            }
        });
    }

    function go(delta) {
        if (items.length < 2) return;
        currentIndex = (currentIndex + delta + items.length) % items.length;
        render();
    }

    function open(list, index) {
        items = list || [];
        currentIndex = clamp(index || 0, 0, Math.max(0, items.length - 1));
        if (!items.length) return;

        buildDomOnce();
        render();
        overlay.classList.add("open");
        document.body.style.overflow = "hidden";
    }

    function close() {
        overlay.classList.remove("open");
        document.body.style.overflow = "";
    }

    function buildDomOnce() {
        if (overlay) return;

        overlay = document.createElement("div");
        overlay.className = "lb-overlay";
        overlay.innerHTML =
            '<div class="lb-topbar">' +
                '<span class="lb-counter"></span>' +
                '<button type="button" class="lb-btn lb-close" title="Close (Esc)">\u2715</button>' +
            '</div>' +
            '<div class="lb-stage">' +
                '<button type="button" class="lb-nav lb-prev" title="Previous (\u2190)">\u2039</button>' +
                '<button type="button" class="lb-nav lb-next" title="Next (\u2192)">\u203A</button>' +
                '<div class="lb-img-wrap"><img alt=""></div>' +
            '</div>' +
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

        bindEvents();
        document.body.appendChild(overlay);
    }

    function bindEvents() {
        overlay.querySelector(".lb-close").addEventListener("click", close);
        prevBtn.addEventListener("click", function () { go(-1); });
        nextBtn.addEventListener("click", function () { go(1); });
        overlay.querySelector(".lb-zoom-in").addEventListener("click", function () { setZoom(scale * 1.25); });
        overlay.querySelector(".lb-zoom-out").addEventListener("click", function () { setZoom(scale / 1.25); });
        overlay.querySelector(".lb-reset").addEventListener("click", function () { resetView(); });

        /* Wheel zoom toward cursor */
        stage.addEventListener("wheel", function (e) {
            e.preventDefault();
            var factor = e.deltaY < 0 ? 1.12 : 1 / 1.12;
            setZoom(scale * factor, e.clientX, e.clientY);
        }, { passive: false });

        /* Double-click toggles fit <-> 2x at pointer */
        stage.addEventListener("dblclick", function (e) {
            if (scale > 1.01) {
                resetView();
            } else {
                setZoom(2, e.clientX, e.clientY);
            }
        });

        /* Drag to pan. dragMoved accumulates the distance so the leftover
           synthetic click after a drag doesn't close the lightbox. */
        var dragging = false, lastX = 0, lastY = 0, dragMoved = 0;
        stage.addEventListener("pointerdown", function (e) {
            if (e.button !== 0 || scale <= 1) return;
            dragging = true;
            dragMoved = 0;
            lastX = e.clientX;
            lastY = e.clientY;
            stage.classList.add("panning");
            try { stage.setPointerCapture(e.pointerId); } catch (err) { }
        });
        stage.addEventListener("pointermove", function (e) {
            if (!dragging) return;
            var dx = e.clientX - lastX;
            var dy = e.clientY - lastY;
            offsetX += dx;
            offsetY += dy;
            dragMoved += Math.abs(dx) + Math.abs(dy);
            lastX = e.clientX;
            lastY = e.clientY;
            applyTransform();
        });
        ["pointerup", "pointercancel"].forEach(function (type) {
            stage.addEventListener(type, function (e) {
                dragging = false;
                stage.classList.remove("panning");
                try { stage.releasePointerCapture(e.pointerId); } catch (err) { }
            });
        });

        /* Pinch zoom on touch devices */
        var pinchStartDist = 0, pinchStartScale = 1;
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

        /* Click on empty space closes — unless the click is the leftover
           synthetic click at the end of a drag-pan. */
        stage.addEventListener("click", function (e) {
            var moved = dragMoved;
            dragMoved = 0;
            if (e.target !== stage || moved > 5) return;
            close();
        });

        /* Keyboard shortcuts */
        document.addEventListener("keydown", function (e) {
            if (!overlay.classList.contains("open")) return;
            if (e.key === "Escape") close();
            else if (e.key === "ArrowLeft") go(-1);
            else if (e.key === "ArrowRight") go(1);
            else if (e.key === "+" || e.key === "=") setZoom(scale * 1.25);
            else if (e.key === "-") setZoom(scale / 1.25);
            else if (e.key === "0") resetView();
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

/* Public shorthand used by views */
function openLightbox(src, alt) {
    Lightbox.openSingle(src, alt);
}

/* Delegated gallery support:
   Any <img data-lightbox="groupName"> opens the lightbox together with
   every other image sharing the same group value on the page. */
document.addEventListener("click", function (e) {
    var img = e.target.closest ? e.target.closest("img[data-lightbox]") : null;
    if (!img) return;

    var group = img.getAttribute("data-lightbox");
    var groupImgs = Array.prototype.slice.call(
        document.querySelectorAll('img[data-lightbox="' + group + '"]')
    );

    var list = groupImgs.map(function (el) {
        return { src: el.currentSrc || el.src, alt: el.alt };
    });

    Lightbox.open(list, groupImgs.indexOf(img));
});
