﻿window.onload = function () {
    let plusSignUrl = window.location.protocol + "//" + window.location.host + "/plusSign.png";
    let noProfilePictureUrl = window.location.protocol + "//" + window.location.host + "/noProfilePicture.png";

    for (let i = 1; i <= 4; i++) {
        let imgElement = document.querySelector('#image' + i);

        if (imgElement === null) {
            continue;
        }

        let currentImageSrc = imgElement.src;


        if (currentImageSrc === plusSignUrl || currentImageSrc === noProfilePictureUrl) {
            continue;
        }

        let currentInput = document.querySelector('#imageInput' + i);
        ConvertBase64ToFile(currentImageSrc, currentInput);

        let button = document.querySelector('#button' + i);
        button.classList.remove("disabled");
    }
}

function ConvertBase64ToFile(base64url, input) {
    var file = dataURLtoFile(base64url, "name.png");
    let container = new DataTransfer();
    container.items.add(file);
    input.files = container.files;
}

function dataURLtoFile(dataurl, filename) {
    var arr = dataurl.split(','),
        mime = arr[0].match(/:(.*?);/)[1],
        bstr = atob(arr[1]),
        n = bstr.length,
        u8arr = new Uint8Array(n);

    while (n--) {
        u8arr[n] = bstr.charCodeAt(n);
    }

    return new File([u8arr], filename, { type: mime });
}