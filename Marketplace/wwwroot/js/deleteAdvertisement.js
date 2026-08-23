let deleteFormActionUrl = document.deleteAdForm.action;

function showDeleteAdModal(title, id) {
    $('.adToDeleteTitle').text(title);

    document.deleteAdForm.action = deleteFormActionUrl + "/" + id;

    showModal('deleteAdModal');
}

function hideDeleteAdModal() {
    hideModal('deleteAdModal');
    document.deleteAdForm.action = deleteFormActionUrl;
}
