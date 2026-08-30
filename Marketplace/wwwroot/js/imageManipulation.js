// Image slots for the Create and Edit ad pages.
// Each page has 1 main image slot (required, slot 1) plus up to 3 optional extra slots (slots 2 to 4), 4 total.
// This file keeps the preview, the remove buttons, the hidden Base64 carriers and the AI button in sync.
// We keep image data client side via Base64 so a validation error does not wipe your picks.

// pendingReads tracks how many FileReaders are still turning a picked file into a data URL.
// We wait for them before letting the form submit, so the hidden Base64 is always ready.
let pendingReads = 0;

// showAiErrorModal
// Shows AI errors in a Bootstrap modal instead of a browser alert.
function showAiErrorModal(message) {
    let modalEl = document.getElementById("aiErrorModal");

    if (!modalEl) {
        // Fallback if modal markup is missing.
        console.error(message);
        return;
    }

    let msgEl = document.getElementById("aiErrorModalMessage");

    if (msgEl) {
        msgEl.textContent = message;
    }

    if (window.bootstrap) {
        bootstrap.Modal.getOrCreateInstance(modalEl).show();
    }
}

document.addEventListener("DOMContentLoaded", function () {
    // On load, check every slot and enable the remove button if needed.
    for (let i = 1; i <= 4; i++) {
        let img = document.getElementById("image" + i);
        let btn = document.getElementById("button" + i);

        if (img && btn) {
            let existingPath = img.getAttribute("data-existing-path");
            let src = img.getAttribute("src");

            let hasStoredImage = existingPath && existingPath.trim() !== "";
            let hasRealSrc = src && !src.endsWith("plusSign.png") && !src.endsWith("noProfilePicture.png");

            if (hasStoredImage || hasRealSrc) {
                btn.removeAttribute("disabled");
                btn.classList.remove("disabled");
            } else {
                btn.setAttribute("disabled", "true");
                btn.classList.add("disabled");
            }
        }
    }

    updateAiButtonState();
    setupCounters();
    setupMainImageRequiredCheck();
});

// setupMainImageRequiredCheck
// Adds a client side check so you get an instant error if you try to create without a main image.
// We check the preview, not just the file input, so a Base64 that survived a post-back also counts.
function setupMainImageRequiredCheck() {
    var form = document.querySelector("form[enctype='multipart/form-data']");

    if (!form) {
        return;
    }

    form.addEventListener("submit", function (e) {
        // If we are still turning a just-picked file into Base64, wait a moment.
        // This keeps optional images from disappearing when you pick and immediately hit Save.
        if (pendingReads > 0) {
            e.preventDefault();

            var checkAndSubmit = function () {
                if (pendingReads > 0) {
                    setTimeout(checkAndSubmit, 50);
                    return;
                }

                // Now hidden Base64 is ready, try submitting again.
                // Use requestSubmit so native validation still runs.
                form.requestSubmit();
            };

            setTimeout(checkAndSubmit, 50);
            return false;
        }

        var mainImg = document.getElementById("image1");
        var src = mainImg ? mainImg.getAttribute("src") : "";

        if (isPlaceholderSrc(src)) {
            var msgSpan = document.querySelector('span[data-valmsg-for="Image"]');

            if (msgSpan) {
                msgSpan.textContent = "The main advertisement image is mandatory.";
                msgSpan.classList.add("field-validation-error");
                msgSpan.classList.remove("field-validation-valid");
            }

            // Also let the browser focus the image area.
            if (mainImg) {
                mainImg.scrollIntoView({ behavior: "smooth", block: "center" });
            }

            e.preventDefault();
            return false;
        }
    });
}

// setupCounters
// Shows live character counts for Title and Description so you know the limits before submit.
function setupCounters() {
    var titleInput = document.getElementById("Title");
    var descInput = document.getElementById("Description");
    var titleCounter = document.getElementById("titleCounter");
    var descCounter = document.getElementById("descriptionCounter");

    if (titleInput && titleCounter) {
        var updateTitle = function () {
            titleCounter.textContent = String(titleInput.value.length);

            if (titleInput.value.length > 35) {
                titleCounter.classList.add("text-danger");
            } else {
                titleCounter.classList.remove("text-danger");
            }
        };

        titleInput.addEventListener("input", updateTitle);
        updateTitle();
    }

    if (descInput && descCounter) {
        var updateDesc = function () {
            descCounter.textContent = String(descInput.value.length);

            if (descInput.value.length > 250) {
                descCounter.classList.add("text-danger");
            } else {
                descCounter.classList.remove("text-danger");
            }
        };

        descInput.addEventListener("input", updateDesc);
        updateDesc();
    }
}

// isPlaceholderSrc
// Returns true if the src is empty or still the default placeholder.
function isPlaceholderSrc(src) {
    if (!src) {
        return true;
    }

    if (src.endsWith("plusSign.png") || src.endsWith("noProfilePicture.png")) {
        return true;
    }

    return false;
}

// getBase64HiddenIds
// Returns the hidden input ids for a given slot index.
// Handles both Create (mainImageBase64) and Edit (editMainBase64) naming.
function getBase64HiddenIds(index) {
    if (index === 1) {
        return {
            base64: document.getElementById("mainImageBase64") || document.getElementById("editMainBase64"),
            fileName: document.getElementById("mainImageFileName") || document.getElementById("editMainFileName")
        };
    }

    if (index === 2) {
        return {
            base64: document.getElementById("additionalBase64_1") || document.getElementById("editAdditionalBase64_1"),
            fileName: document.getElementById("additionalFileName1") || document.getElementById("editAdditionalFileName1")
        };
    }

    if (index === 3) {
        return {
            base64: document.getElementById("additionalBase64_2") || document.getElementById("editAdditionalBase64_2"),
            fileName: document.getElementById("additionalFileName2") || document.getElementById("editAdditionalFileName2")
        };
    }

    if (index === 4) {
        return {
            base64: document.getElementById("additionalBase64_3") || document.getElementById("editAdditionalBase64_3"),
            fileName: document.getElementById("additionalFileName3") || document.getElementById("editAdditionalFileName3")
        };
    }

    return { base64: null, fileName: null };
}

// handleImageClick
// When you click a slot, either open the picker or show the image bigger.
function handleImageClick(index, inputId) {
    let img = document.getElementById("image" + index);

    if (!img) {
        return;
    }

    let src = img.getAttribute("src");

    if (isPlaceholderSrc(src)) {
        var picker = document.getElementById(inputId);

        if (picker) {
            picker.click();
        }

        return;
    }

    // Build a list of all filled images so the lightbox can swipe through them.
    let items = [];
    let currentIndex = 0;

    for (let i = 1; i <= 4; i++) {
        let slotImg = document.getElementById("image" + i);

        if (!slotImg) {
            continue;
        }

        let slotSrc = slotImg.getAttribute("src");

        if (isPlaceholderSrc(slotSrc)) {
            continue;
        }

        if (i === index) {
            currentIndex = items.length;
        }

        items.push({ src: slotSrc, alt: "Image " + i });
    }

    if (window.Lightbox) {
        Lightbox.open(items, currentIndex);
    }
}

// handleFileSelected
// Called when you pick a file. Shows a quick preview and stores a Base64 copy
// in a hidden field so the preview survives a server validation error.
function handleFileSelected(input, index) {
    if (input.files && input.files[0]) {
        var file = input.files[0];

        // Keep the POST small and avoid hitting server limits. 5MB is plenty for a listing photo.
        if (file.size > 5 * 1024 * 1024) {
            showAiErrorModal("Image is too large. Please pick a file smaller than 5MB.");
            input.value = "";
            return;
        }

        pendingReads++;

        var reader = new FileReader();

        reader.onload = function (e) {
            let img = document.getElementById("image" + index);

            if (img) {
                img.setAttribute("src", e.target.result);
                img.setAttribute("data-existing-path", e.target.result);
            }

            var hidden = getBase64HiddenIds(index);

            if (hidden.base64) {
                hidden.base64.value = e.target.result;
            }

            if (hidden.fileName) {
                hidden.fileName.value = file.name;
            }

            // Clear the main image required error once you pick something.
            if (index === 1) {
                var msgSpan = document.querySelector('span[data-valmsg-for="Image"]');

                if (msgSpan) {
                    msgSpan.textContent = "";
                    msgSpan.classList.remove("field-validation-error");
                    msgSpan.classList.add("field-validation-valid");
                }
            }

            pendingReads--;
            updateAiButtonState();
        };

        reader.onerror = function () {
            pendingReads--;
        };

        reader.readAsDataURL(file);

        let btn = document.getElementById("button" + index);

        if (btn) {
            btn.removeAttribute("disabled");
            btn.classList.remove("disabled");
        }

        updateAiButtonState();
    }
}

// deleteImage
// Resets a slot back to the placeholder and clears hidden inputs and Base64 carriers.
function deleteImage(index, hiddenInputId) {
    let isProfile = window.location.pathname.toLowerCase().includes("profile") && index === 1;

    let defaultSrc = "";

    if (isProfile) {
        defaultSrc = "/noProfilePicture.png";
    } else {
        defaultSrc = "/plusSign.png";
    }

    let img = document.getElementById("image" + index);

    if (img) {
        img.setAttribute("src", defaultSrc);
        img.removeAttribute("data-existing-path");
        img.removeAttribute("data-original-path");
    }

    let fileInput = document.getElementById("imageInput" + index);

    if (fileInput) {
        fileInput.value = "";
    }

    if (hiddenInputId) {
        let hiddenInput = document.getElementById(hiddenInputId);

        if (hiddenInput) {
            hiddenInput.value = "";
        }
    }

    // Also clear the Base64 hidden for this slot so we do not resubmit the old preview.
    var hidden = getBase64HiddenIds(index);

    if (hidden.base64) {
        hidden.base64.value = "";
    }

    if (hidden.fileName) {
        hidden.fileName.value = "";
    }

    // For Edit, if we cleared a slot that had an old existing image, keep the cleared state
    // so the server knows to delete it. For Create, just clearing is enough.
    let btn = document.getElementById("button" + index);

    if (btn) {
        btn.setAttribute("disabled", "true");
        btn.classList.add("disabled");
    }

    updateAiButtonState();
}

// updateAiButtonState
// The AI button should only be clickable when the main image (slot 1) exists.
// The main image is required for Title and Category. Extra images (slots 2 to 4) are optional
// and only enrich the Description, so they never enable the button alone.
function updateAiButtonState() {
    let aiBtn = document.getElementById("aiGenerateBtn");

    if (!aiBtn) {
        return;
    }

    let img = document.getElementById("image1");
    let fileInput = document.getElementById("imageInput1");
    let hasMainImage = false;

    if (img) {
        let src = img.getAttribute("src");
        let existingPath = img.getAttribute("data-existing-path");
        let hasFile = fileInput && fileInput.files && fileInput.files.length > 0;
        let hasStored = existingPath && existingPath.trim() !== "";
        let hasRealImage = src && !src.endsWith("plusSign.png") && !src.endsWith("noProfilePicture.png");

        if (hasFile || hasStored || hasRealImage) {
            hasMainImage = true;
        }
    }

    if (hasMainImage) {
        aiBtn.removeAttribute("disabled");
        aiBtn.classList.remove("disabled");
    } else {
        aiBtn.setAttribute("disabled", "true");
        aiBtn.classList.add("disabled");
    }
}

// triggerInputBrowse
// Small helper to open a hidden file input by id.
function triggerInputBrowse(inputId) {
    let input = document.getElementById(inputId);

    if (input) {
        input.click();
    }
}

// triggerSmartListingAI
// Gathers the main image (slot 1, required) plus up to 3 optional extra images (slots 2 to 4) and sends them
// to the AI endpoint in slot order. The main image always goes first and decides Title and Category.
// Tries to reuse already uploaded files when possible to save work.
async function triggerSmartListingAI() {
    // Guard: require the main image. Extras alone must not be analyzed as if they were the main product.
    let mainImg = document.getElementById("image1");
    let mainSrc = mainImg ? mainImg.getAttribute("src") : "";
    let mainPath = mainImg ? mainImg.getAttribute("data-existing-path") : "";
    let mainInput = document.getElementById("imageInput1");
    let hasMainFile = mainInput && mainInput.files && mainInput.files.length > 0;
    let hasMainStored = mainPath && mainPath.trim() !== "";
    let hasMainReal = mainSrc && !mainSrc.endsWith("plusSign.png") && !mainSrc.endsWith("noProfilePicture.png");
    if (!hasMainFile && !hasMainStored && !hasMainReal) {
        showAiErrorModal("Please upload the main image first. The main image is required for AI analysis. Extra images are optional and only add details to the Description.");
        return;
    }

    let formData = new FormData();
    let hasImages = false;

    for (let i = 1; i <= 4; i++) {
        let fileInput = document.getElementById("imageInput" + i);

        // Prefer a file you just picked.
        if (fileInput && fileInput.files && fileInput.files[0]) {
            formData.append("images", fileInput.files[0]);
            hasImages = true;
            continue;
        }

        // Otherwise try to use the preview we already have, even if it is a Base64 data URL.
        let img = document.getElementById("image" + i);
        let existingPath = null;

        if (img) {
            existingPath = img.getAttribute("data-existing-path") || img.getAttribute("src");
        }

        if (existingPath && existingPath.trim() !== "" && !isPlaceholderSrc(existingPath)) {
            // If it is a data URL we already have the bytes, turn it directly into a blob without fetching.
            if (existingPath.startsWith("data:image")) {
                try {
                    let parts = existingPath.split(",");
                    let mime = parts[0].split(":")[1].split(";")[0];
                    let bytes = atob(parts[1]);
                    let ab = new ArrayBuffer(bytes.length);
                    let ia = new Uint8Array(ab);

                    for (let j = 0; j < bytes.length; j++) {
                        ia[j] = bytes.charCodeAt(j);
                    }

                    let blob = new Blob([ab], { type: mime });
                    formData.append("images", blob, "image" + i + ".jpg");
                    hasImages = true;
                    continue;
                } catch (e) {
                    console.error("Could not decode data URL:", e);
                }
            }

            // Otherwise it is a real path on the server, fetch it.
            if (!existingPath.startsWith("data:")) {
                try {
                    let response = await fetch(existingPath);

                    if (response.ok) {
                        let blob = await response.blob();
                        formData.append("images", blob, existingPath.split("/").pop());
                        hasImages = true;
                    } else {
                        console.error("Could not load existing image:", existingPath, response.status);
                    }
                } catch (e) {
                    console.error("Could not load existing image:", existingPath, e);
                }
            }
        }
    }

    if (!hasImages) {
        showAiErrorModal("Please upload at least one image first.");
        return;
    }

    let aiBtn = $("#aiGenerateBtn");
    let originalText = aiBtn.text();
    let originalTitlePlaceholder = $("#Title").attr("placeholder");
    let originalDescriptionPlaceholder = $("#Description").attr("placeholder");

    aiBtn.prop("disabled", true).text("AI is analyzing your images...");
    $("#Title").attr("placeholder", "Analyzing...").prop("disabled", true);
    $("#Description").attr("placeholder", "Analyzing...").prop("disabled", true);

    $.ajax({
        url: "/Advertisement/GenerateListingAI",
        type: "POST",
        data: formData,
        processData: false,
        contentType: false,
        success: function (response) {
            if (response.success && response.data) {
                $("#Title").val(response.data.title);
                $("#Description").val(response.data.description);

                // Category handling respects keep-previous logic.
                // The AI service fetches live categories from the database via CategoryModel and prompts the model
                // with the exact dropdown options. If the AI returns an invalid category (for example a hallucinated id
                // or a misspelled name like furnite), the service returns CategoryId = -1 so we keep the user's
                // previous dropdown selection instead of hardcoding to the first category.
                var newCategoryId = response.data.categoryId;

                if (newCategoryId !== null && newCategoryId !== undefined && newCategoryId !== -1 && newCategoryId !== 0) {
                    var categoryExists = false;
                    var categorySelect = document.getElementById("CategoryId");

                    if (categorySelect) {
                        for (var c = 0; c < categorySelect.options.length; c++) {
                            if (categorySelect.options[c].value === String(newCategoryId)) {
                                categoryExists = true;
                                break;
                            }
                        }
                    }

                    if (categoryExists) {
                        $("#CategoryId").val(newCategoryId).trigger("change");
                    } else {
                        console.warn("AI returned CategoryId " + newCategoryId + " which is not in the dropdown, keeping previous selection");
                    }
                } else {
                    // Keep previous selection. This happens when the AI could not pick a valid category.
                    // We do not overwrite the dropdown so the user's prior choice stays.
                    if (newCategoryId === -1) {
                        console.log("AI could not determine a valid category, keeping previous dropdown selection");
                    }
                }

                // Update counters after AI fills them.
                var ti = document.getElementById("Title");
                var di = document.getElementById("Description");

                if (ti) {
                    ti.dispatchEvent(new Event("input"));
                }

                if (di) {
                    di.dispatchEvent(new Event("input"));
                }
            } else {
                // Friendly fallback. The controller already returns a helpful message. We log the raw response for debugging.
                console.warn("AI GenerateListingAI returned success=false", response);

                showAiErrorModal(response.message || "We could not generate details for this photo. Try again with a clearer photo or fill in the title and category manually.");
            }
        },
        error: function (xhr, status, error) {
            // Network or server error. Keep popup friendly, details go to console for developers.
            console.error("AI GenerateListingAI ajax error:", status, error, xhr ? xhr.responseText : "");

            showAiErrorModal("We could not reach the image helper right now. Please check your connection and try again, or continue by filling the fields manually.");
        },
        complete: function () {
            aiBtn.prop("disabled", false).text(originalText);
            $("#Title").attr("placeholder", originalTitlePlaceholder).prop("disabled", false);
            $("#Description").attr("placeholder", originalDescriptionPlaceholder).prop("disabled", false);

            updateAiButtonState();
        }
    });
}
