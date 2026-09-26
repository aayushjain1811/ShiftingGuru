// Join as Partner page (wwwroot/js/partner-join.js)
//
// 1. GST number shown in capitals while typing
// 2. Document upload tiles (preview, type and size check) - documents are optional
// 3. One submit only (no double clicks)
// 4. Side menu highlights the form section you're in (desktop)
//
// No email or mobile codes any more: the registration fee is the check.

const MAX_FILE_BYTES = 5 * 1024 * 1024; // must match PartnerRegistrationViewModel.MaxFileBytes

const form = document.getElementById("partner-form");

if (form) {
    setUpGst();
    setUpUploads();
    setUpSubmit();
    setUpSectionNav();
}

function setUpGst() {
    const gst = form.querySelector('[name="GstNumber"]');
    if (!gst) return;

    gst.addEventListener("input", () => {
        const cursor = gst.selectionStart;
        gst.value = gst.value.toUpperCase();
        gst.setSelectionRange(cursor, cursor);
    });
}

function setUpUploads() {
    form.querySelectorAll("[data-upload]").forEach((tile) => {
        const input = tile.querySelector('input[type="file"]');
        const box = tile.querySelector("label");
        const icon = tile.querySelector("[data-upload-icon]");
        const preview = tile.querySelector("[data-upload-preview]");
        const name = tile.querySelector("[data-upload-name]");
        const error = tile.querySelector("[data-upload-error]");
        const emptyText = name.textContent;
        let previewUrl = null;

        const clearPreview = () => {
            if (previewUrl) URL.revokeObjectURL(previewUrl);
            previewUrl = null;
            preview.hidden = true;
            preview.removeAttribute("src");
            icon.hidden = false;
            delete box.dataset.state;
        };

        const reject = (message) => {
            input.value = "";
            name.textContent = emptyText;
            error.textContent = message;
        };

        input.addEventListener("change", () => {
            clearPreview();
            error.textContent = "";

            const file = input.files[0];
            if (!file) {
                name.textContent = emptyText;
                return;
            }

            const allowedTypes = input.accept.split(",").map((type) => type.trim());
            if (!allowedTypes.includes(file.type)) {
                reject(input.accept.includes("pdf")
                    ? "Choose a JPG, PNG, WEBP or PDF file."
                    : "Choose a JPG, PNG or WEBP photo.");
                return;
            }

            if (file.size > MAX_FILE_BYTES) {
                reject("This file is larger than 5 MB. Choose a smaller one.");
                return;
            }

            name.textContent = file.name;
            box.dataset.state = "done";

            if (file.type.startsWith("image/")) {
                previewUrl = URL.createObjectURL(file);
                preview.src = previewUrl;
                preview.hidden = false;
                icon.hidden = true;
            }
        });
    });
}

function setUpSubmit() {
    const button = form.querySelector("[data-submit]");
    if (!button) return;
    const label = button.innerHTML;
    let submitting = false;

    form.addEventListener("submit", (event) => {
        if (submitting) {
            event.preventDefault();
            return;
        }
        submitting = true;
        button.disabled = true;
        button.textContent = "Saving your application…";
    });

    // Back button or a server error: the button must work again.
    window.addEventListener("pageshow", () => {
        submitting = false;
        button.disabled = false;
        button.innerHTML = label;
    });
}

function setUpSectionNav() {
    const links = [...document.querySelectorAll("[data-section-link]")];
    if (links.length === 0 || !("IntersectionObserver" in window)) return;

    const setActive = (id) => {
        links.forEach((link) => {
            if (link.getAttribute("href") === `#${id}`) link.dataset.active = "";
            else delete link.dataset.active;
        });
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (entry.isIntersecting) setActive(entry.target.id);
        });
    }, { rootMargin: "-45% 0px -50% 0px" });

    links.forEach((link) => {
        const section = document.querySelector(link.getAttribute("href"));
        if (section) observer.observe(section);
    });

    setActive(links[0].getAttribute("href").slice(1));
}