// Image slots for the Create and Edit ad pages.
// Each page has up to 4 image slots. This file keeps the preview,
// the remove buttons, and the AI button in sync.

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
});

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
// Called when you pick a file. Shows a quick preview right away.
function handleFileSelected(input, index) {
    if (input.files && input.files[0]) {
        var reader = new FileReader();

        reader.onload = function (e) {
            let img = document.getElementById("image" + index);

            if (img) {
                img.setAttribute("src", e.target.result);
            }
        };

        reader.readAsDataURL(input.files[0]);

        let btn = document.getElementById("button" + index);

        if (btn) {
            btn.removeAttribute("disabled");
            btn.classList.remove("disabled");
        }

        updateAiButtonState();
    }
}

// deleteImage
// Resets a slot back to the placeholder and clears the hidden inputs.
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

    let btn = document.getElementById("button" + index);

    if (btn) {
        btn.setAttribute("disabled", "true");
        btn.classList.add("disabled");
    }

    updateAiButtonState();
}

// updateAiButtonState
// The AI button should only be clickable when at least one real image exists.
function updateAiButtonState() {
    let aiBtn = document.getElementById("aiGenerateBtn");

    if (!aiBtn) {
        return;
    }

    let hasValidImage = false;

    for (let i = 1; i <= 4; i++) {
        let img = document.getElementById("image" + i);
        let fileInput = document.getElementById("imageInput" + i);

        if (img) {
            let src = img.getAttribute("src");
            let existingPath = img.getAttribute("data-existing-path");
            let hasFile = fileInput && fileInput.files && fileInput.files.length > 0;

            let hasStored = existingPath && existingPath.trim() !== "";
            let hasRealImage = src && !src.endsWith("plusSign.png") && !src.endsWith("noProfilePicture.png");

            if (hasFile || hasStored || hasRealImage) {
                hasValidImage = true;
                break;
            }
        }
    }

    if (hasValidImage) {
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
// Gathers up to 4 images and sends them to the AI endpoint.
// Tries to reuse already uploaded files when possible to save work.
async function triggerSmartListingAI() {
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

        // Otherwise try to fetch the image that is already on the server.
        let img = document.getElementById("image" + i);
        let existingPath = null;

        if (img) {
            existingPath = img.getAttribute("data-existing-path");
        }

        if (existingPath && existingPath.trim() !== "") {
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

    if (!hasImages) {
        alert("Please upload at least one image first.");
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
                $("#CategoryId").val(response.data.categoryId).trigger("change");
            } else {
                alert(response.message || "AI could not generate listing details. Check server logs.");
            }
        },
        error: function (xhr, status, error) {
            console.error("AI Generation Error:", error);
            alert("Failed to communicate with AI service.");
        },
        complete: function () {
            aiBtn.prop("disabled", false).text(originalText);
            $("#Title").attr("placeholder", originalTitlePlaceholder).prop("disabled", false);
            $("#Description").attr("placeholder", originalDescriptionPlaceholder).prop("disabled", false);

            updateAiButtonState();
        }
    });
}
