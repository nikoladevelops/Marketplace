let plusSignUrl = window.location.protocol + "//" + window.location.host + "/plusSign.png";

function readURL(input, imageId, buttonId) {
    if (input.files && input.files[0]) {
        var reader = new FileReader();

        reader.onload = function (e) {

            $('#' + imageId).attr('src', e.target.result);

        };

        reader.readAsDataURL(input.files[0]);

        let button = document.querySelector("#" + buttonId);
        button.classList.remove("disabled");
    }
    else if (input.files.length === 0) {
        document.querySelector("#" + imageId).src = plusSignUrl;

        let button = document.querySelector("#" + buttonId);
        button.classList.add("disabled");
    }
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

function deleteImage(button, image, input) {
    document.querySelector("#" + image).src = plusSignUrl;
    document.querySelector("#" + input).files = new DataTransfer().files;
    button.classList.add("disabled");
}