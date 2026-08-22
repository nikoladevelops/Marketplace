// Automatically enable delete buttons on page load ONLY if a custom image exists
document.addEventListener("DOMContentLoaded", function () {
    for (let i = 1; i <= 4; i++) {
        let img = document.querySelector("#image" + i);
        let btn = document.querySelector("#button" + i);
        if (img && btn) {
            let src = img.getAttribute("src");

            // Check if source exists and is NOT a default placeholder
            if (src && !src.endsWith("noProfilePicture.png") && !src.endsWith("plusSign.png")) {
                btn.classList.remove("disabled");
            } else {
                btn.classList.add("disabled");
            }
        }
    }
});

function readURL(input, imageId, buttonId) {
    if (input.files && input.files[0]) {
        var reader = new FileReader();
        reader.onload = function (e) {
            $('#' + imageId).attr('src', e.target.result);
        };
        reader.readAsDataURL(input.files[0]);

        let button = document.querySelector("#" + buttonId);
        if (button) button.classList.remove("disabled");
    }
}

function deleteImage(button, imageId, inputId, hiddenInputId) {
    // Determine the correct default placeholder based on context
    let defaultSrc = (window.location.pathname.toLowerCase().includes("profile") && imageId === "image1")
        ? "/noProfilePicture.png"
        : "/plusSign.png";

    // Reset image preview to placeholder
    document.querySelector("#" + imageId).src = defaultSrc;

    // Clear file input selection
    let fileInput = document.querySelector("#" + inputId);
    if (fileInput) fileInput.value = "";

    // Clear hidden database path tracker so backend knows it was deleted
    if (hiddenInputId) {
        let hiddenInput = document.querySelector("#" + hiddenInputId);
        if (hiddenInput) hiddenInput.value = "";
    }

    // Disable delete button
    button.classList.add("disabled");
}

function lowerImageOpacity(x) {
    x.style.opacity = "0.6";
}

function increaseImageOpacity(x) {
    x.style.opacity = "1";
}

function triggerInputBrowse(inputId) {
    $('#' + inputId).trigger('click');
}