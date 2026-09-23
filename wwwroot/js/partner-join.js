// Join as Partner page (wwwroot/js/partner-join.js)
//
// Handles four things on the form:
//   1. GST number shown in capitals while typing
//   2. Document upload tiles (preview, type and size check)
//   3. Email + mobile verification with a one-time code
//   4. Submit button stays locked until both are verified
//   5. Side menu highlights the form section you're in (desktop)
//
// Firebase is only downloaded when someone asks for a mobile code,
// so the rest of the page still works if Google's CDN is slow or blocked.

const FIREBASE_VERSION = "10.12.2";
const MAX_FILE_BYTES = 5 * 1024 * 1024; // must match PartnerRegistrationViewModel.MaxFileBytes

const form = document.getElementById("partner-form");

if (form) {
    setUpGst();
    setUpUploads();
    setUpEmail();
    setUpPhone();
    setUpSubmit();
    setUpSectionNav();
}

// ---------------------------------------------------------------------------
// 1. GST
// ---------------------------------------------------------------------------

function setUpGst() {
    const gst = form.querySelector('[name="GstNumber"]');
    if (!gst) return;

    gst.addEventListener("input", () => {
        const cursor = gst.selectionStart;
        gst.value = gst.value.toUpperCase();
        gst.setSelectionRange(cursor, cursor);
    });
}

// ---------------------------------------------------------------------------
// 2. Upload tiles
// ---------------------------------------------------------------------------

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
            box.dataset.state = "done"; // tile gets the accent border and a tick

            if (file.type.startsWith("image/")) {
                previewUrl = URL.createObjectURL(file);
                preview.src = previewUrl;
                preview.hidden = false;
                icon.hidden = true;
            }
        });
    });
}

// ---------------------------------------------------------------------------
// 3. Verification (shared helper for email and mobile)
// ---------------------------------------------------------------------------

function verifyBox(box) {
    const ui = {
        input: box.querySelector("[data-verify-input]"),
        send: box.querySelector("[data-verify-send]"),
        step: box.querySelector("[data-verify-step]"),
        code: box.querySelector("[data-verify-code]"),
        check: box.querySelector("[data-verify-check]"),
        statusEl: box.querySelector("[data-verify-status]"),
        token: box.querySelector("[data-verify-token]"),
        timer: null
    };

    ui.status = (text, state = "idle") => {
        ui.statusEl.textContent = text;
        ui.statusEl.dataset.state = state;
    };

    // "Resend in 30s" countdown after a code is sent.
    ui.cooldown = (seconds = 30) => {
        clearInterval(ui.timer);
        let left = seconds;
        ui.send.disabled = true;
        ui.send.textContent = `Resend in ${left}s`;

        ui.timer = setInterval(() => {
            left -= 1;
            if (left > 0) {
                ui.send.textContent = `Resend in ${left}s`;
                return;
            }
            clearInterval(ui.timer);
            ui.send.disabled = false;
            ui.send.textContent = "Resend code";
        }, 1000);
    };

    ui.showCodeStep = () => {
        ui.step.hidden = false;
        ui.code.value = "";
        ui.code.focus();
    };

    ui.verified = (token, text) => {
        clearInterval(ui.timer);
        ui.token.value = token;
        ui.step.hidden = true;
        ui.send.disabled = true;
        ui.send.textContent = "Verified";
        ui.status(`✓ ${text}`, "ok");
        box.dataset.state = "ok"; // field border turns green
        form.dispatchEvent(new Event("verification-change"));
    };

    // Editing the email or number after verifying means it must be verified again.
    ui.reset = () => {
        if (!ui.token.value && ui.step.hidden) return;
        clearInterval(ui.timer);
        ui.token.value = "";
        ui.step.hidden = true;
        ui.code.value = "";
        ui.send.disabled = false;
        ui.send.textContent = "Send code";
        ui.status("");
        delete box.dataset.state;
        form.dispatchEvent(new Event("verification-change"));
    };

    // Pressing Enter in the code box verifies instead of submitting the whole form.
    ui.code.addEventListener("keydown", (event) => {
        if (event.key !== "Enter") return;
        event.preventDefault();
        ui.check.click();
    });

    // Page came back from the server with this already verified.
    if (ui.token.value) {
        ui.send.disabled = true;
        ui.send.textContent = "Verified";
    }

    return ui;
}

// ---------------------------------------------------------------------------
// 3a. Email (codes are sent and checked by our own server, via Resend)
// ---------------------------------------------------------------------------

function setUpEmail() {
    const box = form.querySelector('[data-verify="email"]');
    if (!box) return;
    const ui = verifyBox(box);

    ui.input.addEventListener("input", ui.reset);

    ui.send.addEventListener("click", async () => {
        const email = ui.input.value.trim();
        if (!/^\S+@\S+\.\S+$/.test(email)) {
            ui.status("Enter a valid email address first.", "error");
            return;
        }

        ui.send.disabled = true;
        ui.status("Sending code…");

        try {
            const response = await postJson("/join-as-partner/email-code/send", { email });
            if (!response.ok) throw new Error(await readError(response, "Couldn't send the code. Try again."));

            ui.showCodeStep();
            ui.status(`We sent a 6-digit code to ${email}.`);
            ui.cooldown();
        } catch (error) {
            ui.send.disabled = false;
            ui.status(error.message || "Couldn't send the code. Try again.", "error");
        }
    });

    ui.check.addEventListener("click", async () => {
        const email = ui.input.value.trim();
        const code = ui.code.value.trim();
        if (!/^\d{6}$/.test(code)) {
            ui.status("Enter the 6-digit code from your email.", "error");
            return;
        }

        ui.check.disabled = true;

        try {
            const response = await postJson("/join-as-partner/email-code/verify", { email, code });
            if (!response.ok) throw new Error(await readError(response, "That code isn't right. Check it and try again."));

            const data = await response.json();
            ui.verified(data.token, "Email verified");
        } catch (error) {
            ui.status(error.message || "That code isn't right. Check it and try again.", "error");
        } finally {
            ui.check.disabled = false;
        }
    });
}

// ---------------------------------------------------------------------------
// 3b. Mobile (codes are sent and checked by Firebase)
// ---------------------------------------------------------------------------

function setUpPhone() {
    const box = form.querySelector('[data-verify="phone"]');
    if (!box) return;
    const ui = verifyBox(box);

    let verifier = null;
    let confirmation = null;

    ui.input.addEventListener("input", () => {
        confirmation = null;
        ui.reset();
    });

    ui.send.addEventListener("click", async () => {
        const digits = ui.input.value.replace(/\D/g, "").slice(-10);
        if (!/^[6-9]\d{9}$/.test(digits)) {
            ui.status("Enter a valid 10-digit mobile number first.", "error");
            return;
        }

        ui.send.disabled = true;
        ui.status("Sending code…");

        try {
            const { auth, fb } = await loadFirebase();
            verifier ??= new fb.RecaptchaVerifier(auth, "recaptcha-container", { size: "invisible" });
            confirmation = await fb.signInWithPhoneNumber(auth, `+91${digits}`, verifier);

            ui.showCodeStep();
            ui.status(`We sent a 6-digit code to +91 ${digits}.`);
            ui.cooldown();
        } catch (error) {
            // A used or failed reCAPTCHA can't be reused, so start fresh next time.
            verifier?.clear();
            verifier = null;
            ui.send.disabled = false;
            ui.status(firebaseMessage(error), "error");
        }
    });

    ui.check.addEventListener("click", async () => {
        const code = ui.code.value.trim();
        if (!/^\d{6}$/.test(code)) {
            ui.status("Enter the 6-digit code from the SMS.", "error");
            return;
        }
        if (!confirmation) {
            ui.status("Send a code first.", "error");
            return;
        }

        ui.check.disabled = true;

        try {
            const { auth, fb } = await loadFirebase();
            const result = await confirmation.confirm(code);

            // This token proves the number was verified. The server checks it with
            // Firebase when the form is submitted. It stays valid for about an hour.
            const idToken = await result.user.getIdToken();
            await fb.signOut(auth);

            ui.verified(idToken, "Mobile number verified");
        } catch (error) {
            ui.status(firebaseMessage(error), "error");
        } finally {
            ui.check.disabled = false;
        }
    });
}

let firebasePromise = null;

function loadFirebase() {
    firebasePromise ??= (async () => {
        const base = `https://www.gstatic.com/firebasejs/${FIREBASE_VERSION}`;
        const [appModule, authModule] = await Promise.all([
            import(`${base}/firebase-app.js`),
            import(`${base}/firebase-auth.js`)
        ]);

        const app = appModule.initializeApp({
            apiKey: form.dataset.fbApiKey,
            authDomain: form.dataset.fbAuthDomain,
            projectId: form.dataset.fbProjectId
        });

        const auth = authModule.getAuth(app);
        auth.languageCode = "en";
        return { auth, fb: authModule };
    })().catch((error) => {
        firebasePromise = null; // allow a retry
        throw error;
    });

    return firebasePromise;
}

function firebaseMessage(error) {
    switch (error?.code) {
        case "auth/invalid-verification-code":
            return "That code isn't right. Check it and try again.";
        case "auth/code-expired":
            return "That code has expired. Send a new one.";
        case "auth/too-many-requests":
            return "Too many attempts from this device. Wait a while, then try again.";
        case "auth/invalid-phone-number":
            return "That mobile number doesn't look right.";
        case "auth/network-request-failed":
            return "No connection. Check your internet and try again.";
        default:
            return "Couldn't complete that. Try again.";
    }
}

// ---------------------------------------------------------------------------
// 4. Submit
// ---------------------------------------------------------------------------

function setUpSubmit() {
    const button = form.querySelector("[data-submit]");
    const hint = form.querySelector("[data-submit-hint]");
    const emailToken = form.querySelector('[name="EmailVerificationToken"]');
    const phoneToken = form.querySelector('[name="PhoneVerificationToken"]');
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    const refresh = () => {
        const ready = Boolean(emailToken.value && phoneToken.value);
        button.disabled = !ready;
        hint.hidden = ready;
    };

    form.addEventListener("verification-change", refresh);

    form.addEventListener("submit", (event) => {
        // Catch missing files here, because the browser empties file boxes
        // whenever the server sends the page back with an error.
        const missing = [...form.querySelectorAll('[data-upload] input[type="file"]')]
            .filter((input) => input.files.length === 0);

        missing.forEach((input) => {
            input.closest("[data-upload]").querySelector("[data-upload-error]").textContent = "Choose a file.";
        });

        if (missing.length > 0) {
            event.preventDefault();
            missing[0].closest("[data-upload]").scrollIntoView({
                behavior: reduceMotion ? "auto" : "smooth",
                block: "center"
            });
            return;
        }

        button.disabled = true;
        button.textContent = "Submitting application…";
    });

    refresh();
}

// ---------------------------------------------------------------------------
// 5. Section menu (desktop only; hidden on phones)
// ---------------------------------------------------------------------------

function setUpSectionNav() {
    const links = [...document.querySelectorAll("[data-section-link]")];
    if (links.length === 0 || !("IntersectionObserver" in window)) return;

    const setActive = (id) => {
        links.forEach((link) => {
            if (link.getAttribute("href") === `#${id}`) link.dataset.active = "";
            else delete link.dataset.active;
        });
    };

    // A section counts as "current" when it crosses the middle of the screen.
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

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function postJson(url, body) {
    const antiforgery = form.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";

    return fetch(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": antiforgery
        },
        body: JSON.stringify(body)
    });
}

async function readError(response, fallback) {
    try {
        const data = await response.json();
        return data.error || fallback;
    } catch {
        return fallback;
    }
}