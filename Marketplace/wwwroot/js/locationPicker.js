/* ============================================================
   LocationPicker — Leaflet map location picking + autofill
   Modes: seller (click/drag to pick, geolocation detect,
   inline + expanded modal) and buyer (read-only meeting point).
   ============================================================ */
var LocationPicker = (function () {
    "use strict";

    var DEFAULT_CENTER = [42.6977, 23.3219]; // Sofia, Bulgaria
    var REVERSE_GEOCODE_URL = "https://nominatim.openstreetmap.org/reverse?format=jsonv2&zoom=16&addressdetails=1";
    var state = null; // active picker instance

    function el(id) {
        return document.getElementById(id);
    }

    function currentThemeName() {
        return document.documentElement.getAttribute("data-theme") === "light" ? "light" : "dark";
    }

    function makeBaseLayer(theme) {
        if (theme === "dark") {
            return L.tileLayer("https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png", {
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>',
                maxZoom: 19
            });
        }
        return L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
            maxZoom: 19
        });
    }

    function buildLabel(displayName) {
        var parts = String(displayName || "").split(",").map(function (p) { return p.trim(); });
        parts = parts.filter(function (p) { return p.length > 0; }).slice(0, 3);
        return parts.join(", ").substring(0, 100);
    }

    /* Reverse-geocode with a race guard: only the latest request wins. */
    function reverseGeocode(lat, lng, requestId, onDone) {
        fetch(REVERSE_GEOCODE_URL + "&lat=" + encodeURIComponent(lat) + "&lon=" + encodeURIComponent(lng), {
            headers: { "Accept": "application/json" }
        })
        .then(function (r) { return r.ok ? r.json() : null; })
        .then(function (data) {
            if (!state || requestId !== state.requestId) return;
            var label = data && data.display_name ? buildLabel(data.display_name) : lat.toFixed(5) + ", " + lng.toFixed(5);
            onDone(label);
        })
        .catch(function () {
            if (!state || requestId !== state.requestId) return;
            onDone(lat.toFixed(5) + ", " + lng.toFixed(5));
        });
    }

    function setStatus(message, isError) {
        if (!state.statusEl) return;
        state.statusEl.textContent = message || "";
        state.statusEl.classList.toggle("text-danger", !!isError);
    }

    function setInputs(pick) {
        if (!state.input) return;
        state.suppressInputClear = true;
        state.input.value = pick.label;
        state.input.dispatchEvent(new Event("input", { bubbles: true }));
        state.suppressInputClear = false;
        if (state.latInput) state.latInput.value = pick.lat;
        if (state.lngInput) state.lngInput.value = pick.lng;
        setStatus("\uD83D\uDCCD " + pick.label, false);
    }

    function makeDragHandler() {
        return function () {
            var p = this.getLatLng();
            pick(p.lat, p.lng);
        };
    }

    function placeMarker(lat, lng, opts) {
        opts = opts || {};
        state.lat = lat;
        state.lng = lng;

        if (state.marker && state.map.hasLayer(state.marker)) {
            state.marker.setLatLng([lat, lng]);
        } else {
            state.marker = L.marker([lat, lng], { draggable: state.picking }).addTo(state.map);
            if (state.picking) state.marker.on("dragend", makeDragHandler());
        }
        if (state.bigMap) {
            if (state.bigMarker && state.bigMap.hasLayer(state.bigMarker)) {
                state.bigMarker.setLatLng([lat, lng]);
            } else {
                state.bigMarker = L.marker([lat, lng], { draggable: state.picking }).addTo(state.bigMap);
                if (state.picking) state.bigMarker.on("dragend", makeDragHandler());
            }
        }

        state.map.setView([lat, lng], Math.max(state.map.getZoom(), 14));
        if (state.bigMap) state.bigMap.setView([lat, lng], Math.max(state.bigMap.getZoom(), 15));

        // Never re-geocode a restored position — the stored label is authoritative.
        if (!opts.skipGeocode) {
            state.requestId++;
            reverseGeocode(lat, lng, state.requestId, function (label) {
                setInputs({ lat: lat, lng: lng, label: label });
            });
        }
    }

    function pick(lat, lng) {
        state.requestId++;
        placeMarker(lat, lng);
    }

    function initInlineMap(center) {
        state.map = L.map(state.mapEl, {
            center: center,
            zoom: 14,
            scrollWheelZoom: false,
            zoomControl: true,
            attributionControl: true
        });
        state.inlineBase = makeBaseLayer(currentThemeName()).addTo(state.map);

        if (state.picking) {
            state.map.on("click", function (e) {
                pick(e.latlng.lat, e.latlng.lng);
            });
            // Wheel zoom stays off until the user interacts, so the page
            // keeps scrolling normally over the embedded map.
            state.map.on("focus", function () {
                if (!state.map.scrollWheelZoom.enabled()) state.map.scrollWheelZoom.enable();
            });
            state.map.getContainer().addEventListener("mouseleave", function () {
                state.map.scrollWheelZoom.disable();
                state.map.blur();
            });
        }
    }

    function ensureBigMap() {
        if (state.bigMap) return;
        var bigMapEl = el(state.options.modalMapId);
        if (!bigMapEl) return;

        state.bigMap = L.map(bigMapEl, {
            center: state.map.getCenter(),
            zoom: Math.max(state.map.getZoom(), 15),
            scrollWheelZoom: true
        });
        state.bigBase = makeBaseLayer(currentThemeName()).addTo(state.bigMap);

        if (state.picking) {
            state.bigMap.on("click", function (e) {
                pick(e.latlng.lat, e.latlng.lng);
            });
        }
        if (state.lat !== undefined) {
            placeMarker(state.lat, state.lng, { skipGeocode: true });
        }
    }

    function detectLocation() {
        if (!navigator.geolocation) {
            setStatus("Geolocation is not supported by this browser.", true);
            return;
        }
        setStatus("\uD83D\uDCE1 Detecting your location\u2026", false);
        navigator.geolocation.getCurrentPosition(function (pos) {
            pick(pos.coords.latitude, pos.coords.longitude);
        }, function (err) {
            setStatus(err.code === err.PERMISSION_DENIED
                ? "Location permission denied \u2014 click the map instead."
                : "Could not detect your location \u2014 click the map instead.", true);
        }, { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 });
    }

    function applyTheme(theme) {
        if (state.inlineBase && state.map) {
            state.map.removeLayer(state.inlineBase);
            state.inlineBase = makeBaseLayer(theme).addTo(state.map);
        }
        if (state.bigBase && state.bigMap) {
            state.bigMap.removeLayer(state.bigBase);
            state.bigBase = makeBaseLayer(theme).addTo(state.bigMap);
        }
    }

    function init(options) {
        state = {
            options: options,
            picking: options.interactive !== false,
            mapEl: el(options.mapId),
            input: options.inputSelector ? document.querySelector(options.inputSelector) : null,
            latInput: el(options.latInputId),
            lngInput: el(options.lngInputId),
            statusEl: el(options.statusElId),
            map: null,
            bigMap: null,
            marker: null,
            bigMarker: null,
            inlineBase: null,
            bigBase: null,
            requestId: 0,
            suppressInputClear: false
        };

        if (!state.mapEl || typeof L === "undefined") return;

        var hasInitial = options.initial && typeof options.initial.lat === "number"
            && typeof options.initial.lng === "number";

        initInlineMap(hasInitial ? [options.initial.lat, options.initial.lng] : DEFAULT_CENTER);

        if (hasInitial) {
            placeMarker(options.initial.lat, options.initial.lng, { skipGeocode: true });
        }

        if (state.picking) {
            if (options.locateBtnId && el(options.locateBtnId)) {
                el(options.locateBtnId).addEventListener("click", detectLocation);
            }
            if (state.input) {
                state.input.addEventListener("input", function () {
                    if (state.suppressInputClear) return;
                    // Manual typing wins: drop the persisted coordinates.
                    if (state.latInput) state.latInput.value = "";
                    if (state.lngInput) state.lngInput.value = "";
                    setStatus("", false);
                });
            }

            var modalEl = el(options.modalId);
            if (modalEl && window.bootstrap) {
                modalEl.addEventListener("shown.bs.modal", function () {
                    ensureBigMap();
                    state.bigMap.invalidateSize();
                    if (state.lat !== undefined) {
                        state.bigMap.setView([state.lat, state.lng], Math.max(state.bigMap.getZoom(), 15));
                    }
                });
                var useBtn = el(options.useLocationBtnId);
                if (useBtn) {
                    useBtn.addEventListener("click", function () {
                        window.bootstrap.Modal.getOrCreateInstance(modalEl).hide();
                    });
                }
            }
        }

        // The navbar theme toggle exists on every page, buyers included.
        document.addEventListener("themechange", function () {
            applyTheme(currentThemeName());
        });
    }

    return { init: init };
})();
