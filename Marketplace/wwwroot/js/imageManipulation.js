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

function handleImageClick(index, inputId) {
    let img = document.getElementById("image" + index);
    let src = img.getAttribute("src");

    // If empty placeholder, open file upload browser. Otherwise, zoom the image.
    if (!src || src.endsWith("plusSign.png") || src.endsWith("noProfilePicture.png")) {
        document.getElementById(inputId).click();
    } else {
        zoomImage(img);
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

function zoomImage(image) {
    let modalImage = document.getElementById("modalImage");
    if (modalImage && image) {
        modalImage.src = image.src;
        $('#zoomImageModal').modal('show');
    }
}

function closeModal(modalName) {
    $('#' + modalName).modal('hide');
}

function triggerSmartListingAI() {
    let formData = new FormData();
    let hasImages = false;

    for (let i = 1; i <= 4; i++) {
        let fileInput = document.getElementById('imageInput' + i);
        if (fileInput && fileInput.files && fileInput.files[0]) {
            formData.append("images", fileInput.files[0]);
            hasImages = true;
        }
    }

    let aiBtn = $("#aiGenerateBtn");
    let originalText = aiBtn.text();

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
                alert("AI could not generate listing details. Check server logs.");
            }
        },
        error: function (xhr, status, error) {
            console.error("AI Generation Error:", error);
            alert("Failed to communicate with AI service.");
        },
        complete: function () {
            aiBtn.prop("disabled", false).text(originalText);
            $("#Title").attr("placeholder", "Title").prop("disabled", false);
            $("#Description").attr("placeholder", "Provide a detailed description of your item...").prop("disabled", false);
            updateAiButtonState();
        }
    });
}