document.addEventListener("DOMContentLoaded", function () {
    // Inspect all image slots on page load and toggle button availability automatically
    for (let i = 1; i <= 4; i++) {
        let img = document.getElementById("image" + i);
        let btn = document.getElementById("button" + i);
        if (img && btn) {
            let existingPath = img.getAttribute("data-existing-path");
            let src = img.getAttribute("src");

            if ((existingPath && existingPath.trim() !== "") || (src && !src.endsWith("plusSign.png") && !src.endsWith("noProfilePicture.png"))) {
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

function isPlaceholderSrc(src) {
    return !src || src.endsWith("plusSign.png") || src.endsWith("noProfilePicture.png");
}

function handleImageClick(index, inputId) {
    let img = document.getElementById("image" + index);
    if (!img) return;

    let src = img.getAttribute("src");

    // If empty placeholder, open file upload browser. Otherwise, zoom the image.
    if (isPlaceholderSrc(src)) {
        document.getElementById(inputId).click();
        return;
    }

    // Collect every filled slot so the user can flip through them in the lightbox
    let items = [];
    let currentIndex = 0;
    for (let i = 1; i <= 4; i++) {
        let slotImg = document.getElementById("image" + i);
        if (!slotImg) continue;
        let slotSrc = slotImg.getAttribute("src");
        if (isPlaceholderSrc(slotSrc)) continue;
        if (i === index) currentIndex = items.length;
        items.push({ src: slotSrc, alt: "Image " + i });
    }

    if (window.Lightbox) {
        Lightbox.open(items, currentIndex);
    }
}

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

function deleteImage(index, hiddenInputId) {
    let defaultSrc = (window.location.pathname.toLowerCase().includes("profile") && index === 1)
        ? "/noProfilePicture.png"
        : "/plusSign.png";

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

function updateAiButtonState() {
    let aiBtn = document.getElementById("aiGenerateBtn");
    if (!aiBtn) return;

    let hasValidImage = false;
    for (let i = 1; i <= 4; i++) {
        let img = document.getElementById("image" + i);
        let fileInput = document.getElementById("imageInput" + i);
        if (img) {
            let src = img.getAttribute("src");
            let existingPath = img.getAttribute("data-existing-path");
            let hasFile = fileInput && fileInput.files && fileInput.files.length > 0;

            if (hasFile || (existingPath && existingPath.trim() !== "") || (src && !src.endsWith("plusSign.png") && !src.endsWith("noProfilePicture.png"))) {
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

function triggerInputBrowse(inputId) {
    let input = document.getElementById(inputId);
    if (input) {
        input.click();
    }
}

async function triggerSmartListingAI() {
    let formData = new FormData();
    let hasImages = false;

    for (let i = 1; i <= 4; i++) {
        let fileInput = document.getElementById('imageInput' + i);

        // Prefer a freshly picked file
        if (fileInput && fileInput.files && fileInput.files[0]) {
            formData.append("images", fileInput.files[0]);
            hasImages = true;
            continue;
        }

        // Otherwise fall back to the already-uploaded image stored on disk
        let img = document.getElementById('image' + i);
        let existingPath = img ? img.getAttribute("data-existing-path") : null;

        if (existingPath && existingPath.trim() !== "") {
            try {
                let response = await fetch(existingPath);
                if (response.ok) {
                    let blob = await response.blob();
                    formData.append("images", blob, existingPath.split('/').pop());
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

    aiBtn.prop("disabled", true).text("🤖 AI is analyzing your images...");
    $("#Title").attr("placeholder", "Analyzing...").prop("disabled", true);
    $("#Description").attr("placeholder", "Analyzing...").prop("disabled", true);

    $.ajax({
        url: '/Advertisement/GenerateListingAI',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (response) {
            if (response.success && response.data) {
                $("#Title").val(response.data.title);
                $("#Description").val(response.data.description);
                $("#CategoryId").val(response.data.categoryId).trigger('change');
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