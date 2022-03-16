let plusSignUrl = window.location.protocol + "//" + window.location.host+"/plusSign.png";

function readURL(input,imageId) {
    if (input.files && input.files[0]) {
        var reader = new FileReader();

        reader.onload= function (e) {
                
        $('#'+imageId).attr('src', e.target.result);
                
        };

        reader.readAsDataURL(input.files[0]);
    }
    else if(input.files.length===0){
        document.querySelector("#"+imageId).src=plusSignUrl;
    }
}
function lowerImageOpacity(x) {
    x.style.opacity = "0.6";
}

function increaseImageOpacity(x) {
    x.style.opacity = "1";
}

function triggerInputBrowse(inputId)
{
    $('#'+inputId).trigger('click');
}