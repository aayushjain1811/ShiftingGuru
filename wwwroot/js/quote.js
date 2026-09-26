// ShiftingGuru - quote.js (short quote form)
//
// 1. Mobile OTP (Firebase), with the Get Free Quotes button locked until
//    the number is verified (when OTP is switched on)
// 2. Storage: "Moving to" isn't needed, and "Moving from" becomes "Storage location"
// 3. No past dates, and no double submits

const FIREBASE_VERSION = "10.12.2";

const form = document.getElementById("quote-form");

if (form) {
    const requireOtp = form.dataset.requireOtp === "true";
    setUpService();
    setUpDate();
    if (requireOtp) setUpPhone();
    setUpSubmit(requireOtp);
}

// ---------------------------------------------------------------------------
// Service: storage needs one location, everything else needs two
// ---------------------------------------------------------------------------

function setUpService() {
    const select = form.querySelector('[name="ServiceSlug"]');
    const toField = form.querySelector("[data-to-field]");
    const fromLabel = form.querySelector("[data-from-label]");
    if (!select || !toField || !fromLabel) return;

    const apply = () => {
        const storage = select.value === "warehouse-storage";
        toField.hidden = storage;
        toField.querySelector("input").disabled = storage;
        fromLabel.textContent = storage ? "Storage location" : "Moving from";
    };

    select.addEventListener("change", apply);
    apply();
}

// ---------------------------------------------------------------------------
// Date: nothing in the past
// ---------------------------------------------------------------------------

function setUpDate() {
    const date = form.querySelector('[name="MovingDate"]');
    if (!date) return;

    const today = new Date();
    date.min = today.getFullYear() + "-"
        + String(today.getMonth() + 1).padStart(2, "0") + "-"
        + String(today.getDate()).padStart(2, "0");
}

// ---------------------------------------------------------------------------
// Mobile OTP (Firebase)
// ---------------------------------------------------------------------------

function setUpPhone() {
    const box = form.querySelector('[data-verify="phone"]');
    if (!box) return;

    const input = box.querySelector("[data-verify-input]");
    const send = box.querySelector("[data-verify-send]");
    const step = box.querySelector("[data-verify-step]");
    const code = box.querySelector("[data-verify-code]");
    const check = box.querySelector("[data-verify-check]");
    const statusEl = box.querySelector("[data-verify-status]");
    const token = box.querySelector("[data-verify-token]");

    let verifier = null;
    let confirmation = null;
    let timer = null;

    const status = (text, state = "idle") => {
        statusEl.textContent = text;
        statusEl.dataset.state = state;
    };

    const changed = () => form.dispatchEvent(new Event("verification-change"));

    const cooldown = (seconds = 30) => {
        clearInterval(timer);
        let left = seconds;
        send.disabled = true;
        send.textContent = `Resend in ${left}s`;
        timer = setInterval(() => {
            left -= 1;
            if (left > 0) {
                send.textContent = `Resend in ${left}s`;
                return;
            }
            clearInterval(timer);
            send.disabled = false;
            send.textContent = "Resend code";
        }, 1000);
    };

    const verified = (idToken) => {
        clearInterval(timer);
        token.value = idToken;
        step.hidden = true;
        send.disabled = true;
        send.textContent = "Verified";
        box.dataset.state = "ok";
        status("✓ Mobile number verified", "ok");
        changed();
    };

    const reset = () => {
        if (!token.value && step.hidden) return;
        clearInterval(timer);
        token.value = "";
        confirmation = null;
        step.hidden = true;
        code.value = "";
        send.disabled = false;
        send.textContent = "Verify";
        delete box.dataset.state;
        status("");
        changed();
    };

    // Came back from the server with the number already verified.
    if (token.value) {
        send.disabled = true;
        send.textContent = "Verified";
        box.dataset.state = "ok";
    }

    input.addEventListener("input", reset);

    code.addEventListener("keydown", (event) => {
        if (event.key !== "Enter") return;
        event.preventDefault();
        check.click();
    });

    send.addEventListener("click", async () => {
        const digits = input.value.replace(/\D/g, "").slice(-10);
        if (!/^[6-9]\d{9}$/.test(digits)) {
            status("Enter a valid 10-digit mobile number first.", "error");
            return;
        }

        send.disabled = true;
        status("Sending code…");

        try {
            const { auth, fb } = await loadFirebase();
            verifier ??= new fb.RecaptchaVerifier(auth, "recaptcha-container", { size: "invisible" });
            confirmation = await fb.signInWithPhoneNumber(auth, `+91${digits}`, verifier);

            step.hidden = false;
            code.value = "";
            code.focus();
            status(`We sent a 6-digit code to +91 ${digits}.`);
            cooldown();
        } catch (error) {
            console.error("Firebase could not send the code:", error?.code, error?.message);
            verifier?.clear();
            verifier = null;
            send.disabled = false;
            status(firebaseMessage(error), "error");
        }
    });

    check.addEventListener("click", async () => {
        const value = code.value.trim();
        if (!/^\d{6}$/.test(value)) {
            status("Enter the 6-digit code from the SMS.", "error");
            return;
        }
        if (!confirmation) {
            status("Tap Verify to get a code first.", "error");
            return;
        }

        check.disabled = true;
        try {
            const { auth, fb } = await loadFirebase();
            const result = await confirmation.confirm(value);
            const idToken = await result.user.getIdToken();
            await fb.signOut(auth);
            verified(idToken);
        } catch (error) {
            status(firebaseMessage(error), "error");
        } finally {
            check.disabled = false;
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

        // Local testing only: the invisible reCAPTCHA check fails on localhost,
        // so skip it there. While this is on, ONLY Firebase test numbers work.
        if (location.hostname === "localhost") {
            auth.settings.appVerificationDisabledForTesting = true;
        }

        return { auth, fb: authModule };
    })().catch((error) => {
        firebasePromise = null;
        throw error;
    });

    return firebasePromise;
}

function firebaseMessage(error) {
    switch (error?.code) {
        case "auth/invalid-verification-code":
            return "That code isn't right. Check it and try again.";
        case "auth/code-expired":
            return "That code has expired. Tap Resend code.";
        case "auth/too-many-requests":
            return "Too many attempts from this device. Wait a while, then try again.";
        case "auth/invalid-phone-number":
            return "That mobile number doesn't look right.";
        case "auth/network-request-failed":
            return "No connection. Check your internet and try again.";
        default:
            return "Couldn't send the code right now. Please try again.";
    }
}

// ---------------------------------------------------------------------------
// Submit
// ---------------------------------------------------------------------------

function setUpSubmit(requireOtp) {
    const button = form.querySelector("[data-submit]");
    const hint = form.querySelector("[data-submit-hint]");
    const token = form.querySelector('[name="PhoneVerificationToken"]');
    const label = button.innerHTML;
    let submitting = false;

    const refresh = () => {
        const ready = !requireOtp || Boolean(token && token.value);
        button.disabled = !ready;
        if (hint) hint.hidden = ready;
    };

    form.addEventListener("verification-change", refresh);

    form.addEventListener("submit", (event) => {
        if (submitting) {
            event.preventDefault();
            return;
        }
        submitting = true;
        button.disabled = true;
        button.textContent = "Sending your request…";
    });

    // If the page is shown again (back button, server error), the button must work.
    window.addEventListener("pageshow", () => {
        submitting = false;
        button.innerHTML = label;
        refresh();
    });

    refresh();
}