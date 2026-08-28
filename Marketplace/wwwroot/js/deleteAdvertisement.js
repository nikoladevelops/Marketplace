// Helpers for the delete ad confirmation modal.
// We keep the original form action and swap in the id when you click delete.

var deleteFormActionUrl = document.deleteAdForm ? document.deleteAdForm.action : "";

// showDeleteAdModal
// Opens the delete confirmation and points the form to the right ad.
function showDeleteAdModal(title, id) {
    var titleSpans = document.querySelectorAll(".adToDeleteTitle");

    if (titleSpans && titleSpans.length) {
        // jQuery is available on these pages, but we also handle plain DOM.
        if (window.$) {
            $(".adToDeleteTitle").text(title);
        } else {
            titleSpans.forEach(function (el) {
                el.textContent = title;
            });
        }
    }

    if (document.deleteAdForm) {
        document.deleteAdForm.action = deleteFormActionUrl + "/" + id;
    }

    showModal("deleteAdModal");
}

// hideDeleteAdModal
// Closes the modal and puts the form action back to the original.
function hideDeleteAdModal() {
    hideModal("deleteAdModal");

    if (document.deleteAdForm) {
        document.deleteAdForm.action = deleteFormActionUrl;
    }
}
