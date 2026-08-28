// pagination.js - tiny shared helpers for AJAX pagination
// Used by Home and Admin so we do not copy the same 40 lines twice.
// Keeps the logic in one place so fixes only need to happen once.

(function () {
    "use strict";

    // parseMaxPages
    // Reads max pages from either the container dataset or a hidden helper element.
    // Returns 0 if nothing is found, which means no limit.
    function parseMaxPages(container) {
        if (!container) {
            return 0;
        }

        var fromDataset = parseInt(container.dataset.maxPages || "0", 10);

        if (!isNaN(fromDataset) && fromDataset > 0) {
            return fromDataset;
        }

        var helper = container.querySelector("#userListMaxPages") || document.getElementById("userListMaxPages");

        if (helper) {
            var fromHelper = parseInt(helper.getAttribute("data-max-pages") || "0", 10);

            if (!isNaN(fromHelper) && fromHelper > 0) {
                return fromHelper;
            }
        }

        // Fallback to the hidden span inside the fetched HTML is handled by the caller.
        return 0;
    }

    // updateContainerPagingFromHtml
    // After an AJAX fetch, pull the new page and maxPages from the returned HTML.
    // Updates both dataset.page and dataset.maxPages so the next click has fresh bounds.
    function updateContainerPagingFromHtml(container, html, fallbackPage) {
        if (!container) {
            return;
        }

        var temp = document.createElement("div");
        temp.innerHTML = html;

        var maxPagesEl = temp.querySelector("#userListMaxPages");

        if (maxPagesEl) {
            var rawMax = maxPagesEl.getAttribute("data-max-pages") || "0";
            var parsedMax = parseInt(rawMax, 10);

            if (!isNaN(parsedMax)) {
                container.dataset.maxPages = String(parsedMax);
            }
        } else {
            // Home grid does not have that helper, so try to infer from pagination.
            // If there is no pagination at all, keep the old value.
            var anyPageLink = temp.querySelector("[data-page]");

            if (!anyPageLink) {
                // No pages in response means maxPages might be 0 or 1, keep as is
                // unless we can see the empty state.
                var empty = temp.textContent || "";

                if (empty.indexOf("No advertisements") !== -1 || empty.indexOf("No users") !== -1) {
                    container.dataset.maxPages = "0";
                }
            }
        }

        var active = temp.querySelector(".pagination-links .active");

        if (active) {
            var cur = parseInt(active.getAttribute("data-page") || active.textContent, 10);

            if (!isNaN(cur)) {
                container.dataset.page = String(cur);
                return;
            }
        }

        // Fallback to the page we asked for.
        if (fallbackPage != null) {
            container.dataset.page = String(fallbackPage);
        }
    }

    // isValidPage
    // Checks if a page number is inside the allowed range.
    function isValidPage(page, maxPages) {
        if (isNaN(page)) {
            return false;
        }

        if (page < 0) {
            return false;
        }

        if (maxPages > 0 && page >= maxPages) {
            return false;
        }

        return true;
    }

    // attachPaginationHandler
    // Wires up clicks on a[data-page] inside a container.
    // containerSelector is e.g. "#adGridContainer" or "#userListContainer"
    // fetchFn is the function to call with the page number, like fetchGrid or fetchUserList.
    function attachPaginationHandler(containerSelector, fetchFn) {
        var container = document.querySelector(containerSelector);

        if (!container) {
            return;
        }

        container.addEventListener("click", function (e) {
            var link = e.target.closest("a[data-page]");

            if (!link) {
                return;
            }

            if (link.classList.contains("disabled") || link.getAttribute("aria-disabled") === "true") {
                e.preventDefault();
                return;
            }

            e.preventDefault();

            var page = parseInt(link.getAttribute("data-page"), 10);
            var maxPages = parseInt(container.dataset.maxPages || "0", 10);

            if (!isValidPage(page, maxPages)) {
                return;
            }

            fetchFn(page);
        });
    }

    window.Pagination = {
        parseMaxPages: parseMaxPages,
        updateContainerPagingFromHtml: updateContainerPagingFromHtml,
        isValidPage: isValidPage,
        attachPaginationHandler: attachPaginationHandler
    };
})();
