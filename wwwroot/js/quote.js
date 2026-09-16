/* ---------------------------------------------------------------
   ShiftingGuru - quote.js
   Drives the multi-step quote form. Without this file the form still
   works: every step is visible and it submits as one long form.
   --------------------------------------------------------------- */
(function () {
  "use strict";

  var form = document.getElementById("quote-form");
  if (!form) return;

  // Switching this class on is what collapses the form into steps. It is
  // added by JavaScript, so no-JS users never see a half-hidden form.
  document.documentElement.classList.add("js-steps");

  var LAST_STEP = 5;          // step 5 is the review panel
  var current = 1;

  var panels = {};
  form.querySelectorAll("[data-step]").forEach(function (panel) {
    panels[panel.dataset.step] = panel;
  });

  var backBtn = form.querySelector("[data-nav-back]");
  var nextBtn = form.querySelector("[data-nav-next]");
  var submitBtn = form.querySelector("[data-nav-submit]");
  var stepCurrent = document.querySelector("[data-step-current]");
  var stepBar = document.querySelector("[data-step-bar]");
  var markers = document.querySelectorAll("[data-step-marker]");
  var summary = form.querySelector("[data-summary]");

  /* -------------------------------------------------------------
     Which service is selected
     ------------------------------------------------------------- */
  function selectedService() {
    var checked = form.querySelector("[data-service-option]:checked");
    return checked ? checked.value : null;
  }

  function selectedServiceName() {
    var checked = form.querySelector("[data-service-option]:checked");
    if (!checked) return "";
    var label = checked.closest("label");
    var name = label ? label.querySelector(".font-display") : null;
    return name ? name.textContent.trim() : checked.value;
  }

  /* -------------------------------------------------------------
     Show only the field groups that belong to the chosen service
     ------------------------------------------------------------- */
  function applyServiceFields() {
    var slug = selectedService();
    var isStorage = slug === "warehouse-storage";

    form.querySelectorAll("[data-location-group]").forEach(function (group) {
      var wanted = group.dataset.locationGroup === (isStorage ? "storage" : "route");
      group.hidden = !wanted;
      disableInside(group, !wanted);
    });

    form.querySelectorAll("[data-requirements-group]").forEach(function (group) {
      var wanted = group.dataset.requirementsGroup === slug;
      group.hidden = !wanted;
      disableInside(group, !wanted);
    });

    // The date means something different for storage.
    var dateLabel = form.querySelector("[data-date-label]");
    if (dateLabel) {
      dateLabel.textContent = isStorage ? "Storage start date" : "Moving date";
    }
  }

  // Hidden fields are disabled so the browser skips them and the server
  // never receives values from a service the user didn't pick.
  function disableInside(group, disabled) {
    group.querySelectorAll("input, select, textarea").forEach(function (field) {
      field.disabled = disabled;
    });
  }

  /* -------------------------------------------------------------
     Step navigation
     ------------------------------------------------------------- */
  function show(step, moveFocus) {
    current = step;

    Object.keys(panels).forEach(function (key) {
      panels[key].hidden = Number(key) !== step;
    });

    backBtn.hidden = step === 1;
    nextBtn.hidden = step === LAST_STEP;
    submitBtn.hidden = step !== LAST_STEP;

    var displayStep = Math.min(step, 4);
    if (stepCurrent) stepCurrent.textContent = displayStep;
    if (stepBar) stepBar.style.width = (displayStep * 25) + "%";

    markers.forEach(function (marker) {
      var n = Number(marker.dataset.stepMarker);
      marker.classList.toggle("is-active", n === displayStep);
      marker.classList.toggle("is-done", n < displayStep);
    });

    if (step === LAST_STEP) buildSummary();

    // Focus the panel so screen readers announce the new step.
    if (moveFocus !== false) panels[step].focus();
  }

  /* -------------------------------------------------------------
     Per-step validation. Deliberately shallow: it only blocks obvious
     gaps. The server re-checks everything on submit.
     ------------------------------------------------------------- */
  function validateStep(step) {
    var panel = panels[step];
    var problems = [];

    if (step === 1 && !selectedService()) {
      problems.push({ field: null, message: "Please choose a service." });
    }

    if (step === 2) {
      requireText(panel, "MovingFrom", "Enter the city you are moving from.", problems);
      requireText(panel, "MovingTo", "Enter the city you are moving to.", problems);
      requireText(panel, "StorageLocation", "Where do you need storage?", problems);
      requireText(panel, "MovingDate", "Pick an approximate date.", problems);
    }

    if (step === 3) {
      requireGroup(panel, "PropertyType", "Select your property type.", problems);
      requireGroup(panel, "OfficeSize", "Select your office size.", problems);
      requireGroup(panel, "VehicleType", "Select the vehicle type.", problems);
      requireGroup(panel, "GoodsType", "Select the type of goods.", problems);
      requireGroup(panel, "StorageType", "Select the storage type.", problems);
      requireGroup(panel, "StorageDuration", "Select how long you need it.", problems);
    }

    if (step === 4) {
      requireText(panel, "CustomerName", "Please enter your name.", problems);

      var phone = panel.querySelector('[name="Phone"]');
      if (phone && !phone.disabled) {
        var value = phone.value.trim();
        if (!value) {
          setError(phone, "Please enter your mobile number.");
          problems.push({ field: phone });
        } else if (!/^(\+?91[\s\-]?|0)?[6-9]\d{9}$/.test(value)) {
          setError(phone, "Enter a valid 10-digit Indian mobile number.");
          problems.push({ field: phone });
        } else {
          clearError(phone);
        }
      }

      var email = panel.querySelector('[name="Email"]');
      if (email && email.value.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim())) {
        setError(email, "Enter a valid email address.");
        problems.push({ field: email });
      } else if (email) {
        clearError(email);
      }
    }

    if (problems.length) {
      var first = problems[0].field;
      if (first) first.focus();
      else panel.focus();
      return false;
    }

    return true;
  }

  // A text field only counts if it is actually in play (not disabled by
  // the service-field switching above).
  function requireText(panel, name, message, problems) {
    var field = panel.querySelector('[name="' + name + '"]');
    if (!field || field.disabled) return;

    if (!field.value.trim()) {
      setError(field, message);
      problems.push({ field: field });
    } else {
      clearError(field);
    }
  }

  function requireGroup(panel, name, message, problems) {
    var options = panel.querySelectorAll('[name="' + name + '"]');
    if (!options.length || options[0].disabled) return;

    var chosen = panel.querySelector('[name="' + name + '"]:checked');
    if (!chosen) {
      var fieldset = options[0].closest("fieldset");
      if (fieldset) setGroupError(fieldset, message);
      problems.push({ field: options[0] });
    } else {
      var fs = options[0].closest("fieldset");
      if (fs) clearGroupError(fs);
    }
  }

  /* -------------------------------------------------------------
     Error display. Reuses the same <span> the server writes into, so
     client and server errors look identical.
     ------------------------------------------------------------- */
  function errorSpanFor(field) {
    var container = field.closest("div, fieldset");
    return container ? container.querySelector("span.text-red-600") : null;
  }

  function setError(field, message) {
    field.setAttribute("aria-invalid", "true");
    field.classList.add("border-red-500");
    var span = errorSpanFor(field);
    if (span) span.textContent = message;
  }

  function clearError(field) {
    field.removeAttribute("aria-invalid");
    field.classList.remove("border-red-500");
    var span = errorSpanFor(field);
    if (span) span.textContent = "";
  }

  function setGroupError(fieldset, message) {
    var span = fieldset.querySelector("span.text-red-600");
    if (span) span.textContent = message;
  }

  function clearGroupError(fieldset) {
    var span = fieldset.querySelector("span.text-red-600");
    if (span) span.textContent = "";
  }

  /* -------------------------------------------------------------
     Review summary
     ------------------------------------------------------------- */
  function buildSummary() {
    if (!summary) return;
    summary.textContent = "";

    var rows = [["Service", selectedServiceName()]];

    var isStorage = selectedService() === "warehouse-storage";
    if (isStorage) {
      rows.push(["Location", valueOf("StorageLocation")]);
      rows.push(["Start date", formatDate(valueOf("MovingDate"))]);
      rows.push(["Storage type", checkedValue("StorageType")]);
      rows.push(["Duration", checkedValue("StorageDuration")]);
    } else {
      rows.push(["From", valueOf("MovingFrom")]);
      rows.push(["To", valueOf("MovingTo")]);
      rows.push(["Date", formatDate(valueOf("MovingDate"))]);
    }

    ["PropertyType", "OfficeSize", "VehicleType", "GoodsType"].forEach(function (name) {
      var value = checkedValue(name);
      if (value) rows.push(["Requirements", value]);
    });

    rows.push(["Name", valueOf("CustomerName")]);
    rows.push(["Phone", valueOf("Phone")]);

    var email = valueOf("Email");
    if (email) rows.push(["Email", email]);

    rows.forEach(function (row) {
      if (!row[1]) return;

      var wrapper = document.createElement("div");
      wrapper.className = "flex flex-wrap items-baseline justify-between gap-4 py-4";

      var term = document.createElement("dt");
      term.className = "text-sm text-ink-500";
      term.textContent = row[0];

      var value = document.createElement("dd");
      value.className = "font-display text-base font-semibold tracking-[-0.02em] text-ink-900";
      value.textContent = row[1];

      wrapper.appendChild(term);
      wrapper.appendChild(value);
      summary.appendChild(wrapper);
    });
  }

  function valueOf(name) {
    var field = form.querySelector('[name="' + name + '"]');
    return field && !field.disabled ? field.value.trim() : "";
  }

  function checkedValue(name) {
    var chosen = form.querySelector('[name="' + name + '"]:checked');
    return chosen && !chosen.disabled ? chosen.value : "";
  }

  function formatDate(iso) {
    if (!iso) return "";
    var parts = iso.split("-");
    if (parts.length !== 3) return iso;
    var date = new Date(parts[0], parts[1] - 1, parts[2]);
    if (isNaN(date.getTime())) return iso;
    return date.toLocaleDateString("en-IN", { day: "numeric", month: "long", year: "numeric" });
  }

  /* -------------------------------------------------------------
     Wiring
     ------------------------------------------------------------- */
  nextBtn.addEventListener("click", function () {
    if (!validateStep(current)) return;
    if (current < LAST_STEP) show(current + 1);
  });

  backBtn.addEventListener("click", function () {
    if (current > 1) show(current - 1);
  });

  // Choosing a service updates step 2 and 3 immediately.
  form.addEventListener("change", function (event) {
    if (event.target.matches("[data-service-option]")) applyServiceFields();
  });

  // Swap the two city fields.
  var swap = form.querySelector("[data-swap-locations]");
  if (swap) {
    swap.addEventListener("click", function () {
      var from = form.querySelector('[name="MovingFrom"]');
      var to = form.querySelector('[name="MovingTo"]');
      if (!from || !to) return;
      var held = from.value;
      from.value = to.value;
      to.value = held;
      from.focus();
    });
  }

  // Double-submit guard. The button is disabled the moment the form is
  // submitted, so an impatient double-click sends one request, not two.
  // The server has its own check as well - this is only the first line.
  var submitting = false;
  form.addEventListener("submit", function (event) {
    if (submitting) {
      event.preventDefault();
      return;
    }
    submitting = true;
    submitBtn.disabled = true;
    submitBtn.classList.add("opacity-60", "cursor-not-allowed");
    submitBtn.textContent = "Sending your request...";
  });

  // If the server sends the page back with errors, the button must work again.
  window.addEventListener("pageshow", function () {
    submitting = false;
    submitBtn.disabled = false;
    submitBtn.classList.remove("opacity-60", "cursor-not-allowed");
    submitBtn.textContent = "Submit Request";
  });

  // Stop past dates being picked.
  var dateField = form.querySelector('[data-min-today="true"]');
  if (dateField) {
    var today = new Date();
    dateField.min = today.getFullYear() +
      "-" + String(today.getMonth() + 1).padStart(2, "0") +
      "-" + String(today.getDate()).padStart(2, "0");
  }

  /* -------------------------------------------------------------
     Opening state
     ------------------------------------------------------------- */
  applyServiceFields();

  // If the server sent the page back with errors, open the step holding
  // the first one rather than dumping the user at step 1.
  var firstError = form.querySelector("span.text-red-600:not(:empty)");
  if (firstError) {
    var panel = firstError.closest("[data-step]");
    show(panel ? Number(panel.dataset.step) : 1, true);
  } else {
    // Arriving with ?service=... already chosen skips step 1.
    show(selectedService() ? 2 : 1, false);
  }
})();