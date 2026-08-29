// LocationPicker - handles the Leaflet maps for picking a location.
// Two modes: seller mode lets you click and drag to choose a meeting point,
// buyer mode is read only and just shows where to meet.
// Works with both the small inline map and the bigger modal map.

var LocationPicker = (function () {
    "use strict";

    var DEFAULT_CENTER = [42.6977, 23.3219];
    var REVERSE_GEOCODE_URL = "https://nominatim.openstreetmap.org/reverse?format=jsonv2&zoom=16&addressdetails=1";
    var state = null;

    // el
    // Quick shortcut for getElementById.
    function el(id) {
        return document.getElementById(id);
    }

    // makeBaseLayer
    // Always returns the standard light OSM tiles.
    // We keep the same tiles for both themes so no API key is needed.
    function makeBaseLayer() {
        return L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
            maxZoom: 19
        });
    }

    // buildLabel
    // Takes the long address from nominatim and shortens it to a tidy label.
    function buildLabel(displayName) {
        var parts = String(displayName || "").split(",").map(function (p) {
            return p.trim();
        });

        parts = parts.filter(function (p) {
            return p.length > 0;
        }).slice(0, 3);

        return parts.join(", ").substring(0, 100);
    }

    // reverseGeocode
    // Turns lat/lng into a readable address. Only the latest request wins,
    // older ones are ignored so we do not show a stale label.
    function reverseGeocode(lat, lng, requestId, onDone) {
        fetch(REVERSE_GEOCODE_URL + "&lat=" + encodeURIComponent(lat) + "&lon=" + encodeURIComponent(lng), {
            headers: { "Accept": "application/json" }
        })
        .then(function (r) {
            if (r.ok) {
                return r.json();
            } else {
                return null;
            }
        })
        .then(function (data) {
            if (!state || requestId !== state.requestId) {
                return;
            }

            var label = "";

            if (data && data.display_name) {
                label = buildLabel(data.display_name);
            } else {
                label = lat.toFixed(5) + ", " + lng.toFixed(5);
            }

            onDone(label);
        })
        .catch(function () {
            if (!state || requestId !== state.requestId) {
                return;
            }

            onDone(lat.toFixed(5) + ", " + lng.toFixed(5));
        });
    }

    // setStatus
    // Shows a small message under the map, red if it is an error.
    function setStatus(message, isError) {
        if (!state.statusEl) {
            return;
        }

        state.statusEl.textContent = message || "";
        state.statusEl.classList.toggle("text-danger", !!isError);
    }

    // setInputs
    // Fills the text input and hidden lat/lng fields after a pick.
    function setInputs(pick) {
        if (!state.input) {
            return;
        }

        state.suppressInputClear = true;
        state.input.value = pick.label;
        state.input.dispatchEvent(new Event("input", { bubbles: true }));
        state.suppressInputClear = false;

        if (state.latInput) {
            state.latInput.value = pick.lat;
        }

        if (state.lngInput) {
            state.lngInput.value = pick.lng;
        }

        setStatus("\uD83D\uDCCD " + pick.label, false);
    }

    // makeDragHandler
    // Returns a handler that reacts to dragging the marker.
    function makeDragHandler() {
        return function () {
            var p = this.getLatLng();
            pick(p.lat, p.lng);
        };
    }

    // placeMarker
    // Puts or moves the pin on the map. Keeps both maps in sync.
    function placeMarker(lat, lng, opts) {
        opts = opts || {};

        state.lat = lat;
        state.lng = lng;

        if (state.marker && state.map.hasLayer(state.marker)) {
            state.marker.setLatLng([lat, lng]);
        } else {
            state.marker = L.marker([lat, lng], { draggable: state.picking }).addTo(state.map);

            if (state.picking) {
                state.marker.on("dragend", makeDragHandler());
            }
        }

        if (state.bigMap) {
            if (state.bigMarker && state.bigMap.hasLayer(state.bigMarker)) {
                state.bigMarker.setLatLng([lat, lng]);
            } else {
                state.bigMarker = L.marker([lat, lng], { draggable: state.picking }).addTo(state.bigMap);

                if (state.picking) {
                    state.bigMarker.on("dragend", makeDragHandler());
                }
            }
        }

        state.map.setView([lat, lng], Math.max(state.map.getZoom(), 14));

        if (state.bigMap) {
            state.bigMap.setView([lat, lng], Math.max(state.bigMap.getZoom(), 15));
        }

        // If this is a saved position, do not re-geocode. The stored label is already good.
        if (!opts.skipGeocode) {
            state.requestId++;

            reverseGeocode(lat, lng, state.requestId, function (label) {
                setInputs({ lat: lat, lng: lng, label: label });
            });
        }
    }

    // pick
    // Handles a new lat/lng pick from a click or drag.
    function pick(lat, lng) {
        state.requestId++;
        placeMarker(lat, lng);
    }

    // initInlineMap
    // Sets up the small map you see directly on the form.
    function initInlineMap(center) {
        state.map = L.map(state.mapEl, {
            center: center,
            zoom: 14,
            scrollWheelZoom: false,
            zoomControl: true,
            attributionControl: true
        });

        state.inlineBase = makeBaseLayer().addTo(state.map);

        if (state.picking) {
            state.map.on("click", function (e) {
                pick(e.latlng.lat, e.latlng.lng);
            });

            // Wheel zoom stays off until you focus the map, so the page
            // still scrolls normally when you roll over the map.
            state.map.on("focus", function () {
                if (!state.map.scrollWheelZoom.enabled()) {
                    state.map.scrollWheelZoom.enable();
                }
            });

            state.map.getContainer().addEventListener("mouseleave", function () {
                state.map.scrollWheelZoom.disable();
                state.map.blur();
            });
        }
    }

    // ensureBigMap
    // Creates the larger modal map the first time you open it.
    function ensureBigMap() {
        if (state.bigMap) {
            return;
        }

        var bigMapEl = el(state.options.modalMapId);

        if (!bigMapEl) {
            return;
        }

        state.bigMap = L.map(bigMapEl, {
            center: state.map.getCenter(),
            zoom: Math.max(state.map.getZoom(), 15),
            scrollWheelZoom: true
        });

        state.bigBase = makeBaseLayer().addTo(state.bigMap);

        if (state.picking) {
            state.bigMap.on("click", function (e) {
                pick(e.latlng.lat, e.latlng.lng);
            });
        }

        if (state.lat !== undefined) {
            placeMarker(state.lat, state.lng, { skipGeocode: true });
        }
    }

    // detectLocation
    // Tries to use the browser geolocation to find you.
    function detectLocation() {
        if (!navigator.geolocation) {
            setStatus("Geolocation is not supported by this browser.", true);
            return;
        }

        setStatus("\uD83D\uDCE1 Detecting your location...", false);

        navigator.geolocation.getCurrentPosition(function (pos) {
            pick(pos.coords.latitude, pos.coords.longitude);
        }, function (err) {
            if (err.code === err.PERMISSION_DENIED) {
                setStatus("Location permission denied - click the map instead.", true);
            } else {
                setStatus("Could not detect your location - click the map instead.", true);
            }
        }, { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 });
    }

    // applyTheme
    // Kept for compatibility but no longer swaps tiles.
    // Maps stay on the standard light style in both themes.
    function applyTheme() {
        return;
    }

    // init
    // Starts everything. Call this with the ids of your elements.
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

        if (!state.mapEl || typeof L === "undefined") {
            return;
        }

        var hasInitial = options.initial && typeof options.initial.lat === "number" && typeof options.initial.lng === "number";

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
                    if (state.suppressInputClear) {
                        return;
                    }

                    // If you type manually, we clear the lat/lng so they do not get out of sync.
                    if (state.latInput) {
                        state.latInput.value = "";
                    }

                    if (state.lngInput) {
                        state.lngInput.value = "";
                    }

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

        // Theme handling is no longer needed for maps.
    }

    return { init: init };
})();
