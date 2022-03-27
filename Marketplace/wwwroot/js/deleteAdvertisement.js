let deleteFormActionUrl = document.deleteAdForm.action;

function showDeleteAdModal(title, id) {
    $('.adToDeleteTitle').text(title);

    document.deleteAdForm.action = deleteFormActionUrl + "/" + id;

    $('#deleteAdModal').modal('show');
}

function hideDeleteAdModal() {
    $('#deleteAdModal').modal('hide');
    document.deleteAdForm.action = deleteFormActionUrl;
}