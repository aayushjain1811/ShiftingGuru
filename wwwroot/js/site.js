/* ---------------------------------------------------------------
   ShiftingGuru - site.js
   Plain JavaScript, no libraries.
   Every block is independent and exits quietly if its elements are
   missing, so one broken section can never take down the others.
   --------------------------------------------------------------- */
(function () {
  "use strict";

  // Marks that JavaScript is available. app.css hides things only
  // inside ".js", so the page degrades gracefully without this.
  document.documentElement.classList.add("js");

  var reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  document.addEventListener("DOMContentLoaded", function () {
    initMobileMenu();
    initFaq();
    initReveal();
    initCounters();
    initQuoteForm();
    initHeader();
  });

  /* -------------------------------------------------------------
     0. Condensing header
     A 1px marker is dropped just below the fold of the navbar. Once it
     scrolls out of view the header switches to its solid state. No
     scroll listener, so nothing runs on every frame.
     ------------------------------------------------------------- */
  function initHeader() {
    var header = document.getElementById("site-header");
    if (!header || !("IntersectionObserver" in window)) return;

    var sentinel = document.createElement("div");
    sentinel.setAttribute("aria-hidden", "true");
    sentinel.style.cssText =
      "position:absolute;top:88px;left:0;width:1px;height:1px;pointer-events:none;";
    document.body.prepend(sentinel);

    new IntersectionObserver(function (entries) {
      header.classList.toggle("is-condensed", !entries[0].isIntersecting);
    }).observe(sentinel);
  }

  /* -------------------------------------------------------------
     1. Mobile navigation drawer
     ------------------------------------------------------------- */
  function initMobileMenu() {
    var toggle = document.getElementById("menu-toggle");
    var drawer = document.getElementById("mobile-drawer");
    var overlay = document.getElementById("mobile-overlay");
    var closeBtn = document.getElementById("menu-close");

    if (!toggle || !drawer || !overlay) return;

    function open() {
      overlay.hidden = false;
      drawer.hidden = false;
      // Next frame, so the browser registers the starting position
      // before the transform transition begins.
      requestAnimationFrame(function () {
        drawer.classList.add("is-open");
      });
      toggle.setAttribute("aria-expanded", "true");
      document.body.style.overflow = "hidden";
      if (closeBtn) closeBtn.focus();
    }

    function close() {
      drawer.classList.remove("is-open");
      toggle.setAttribute("aria-expanded", "false");
      document.body.style.overflow = "";
      toggle.focus();

      window.setTimeout(function () {
        drawer.hidden = true;
        overlay.hidden = true;
      }, reducedMotion ? 0 : 250);
    }

    toggle.addEventListener("click", open);
    overlay.addEventListener("click", close);
    if (closeBtn) closeBtn.addEventListener("click", close);

    // Any link inside the drawer closes it.
    drawer.addEventListener("click", function (event) {
      if (event.target.closest("a")) close();
    });

    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape" && !drawer.hidden) close();
    });
  }

  /* -------------------------------------------------------------
     2. FAQ accordion
     One listener on the container (event delegation) instead of one
     per question. Buttons give keyboard support for free.
     ------------------------------------------------------------- */
  function initFaq() {
    var list = document.getElementById("faq-list");
    if (!list) return;

    list.addEventListener("click", function (event) {
      var button = event.target.closest("[data-faq-question]");
      if (!button) return;

      var panel = document.getElementById(button.getAttribute("aria-controls"));
      if (!panel) return;

      var isOpen = button.getAttribute("aria-expanded") === "true";

      // Close whichever item is open, so only one animates at a time.
      list.querySelectorAll('[data-faq-question][aria-expanded="true"]').forEach(function (other) {
        if (other === button) return;
        other.setAttribute("aria-expanded", "false");
        var otherPanel = document.getElementById(other.getAttribute("aria-controls"));
        if (otherPanel) otherPanel.classList.remove("is-open");
      });

      button.setAttribute("aria-expanded", isOpen ? "false" : "true");
      panel.classList.toggle("is-open", !isOpen);
    });
  }

  /* -------------------------------------------------------------
     3. Scroll reveal
     One shared observer. Each element is unobserved after it fires,
     so nothing keeps running in the background.
     ------------------------------------------------------------- */
  function initReveal() {
    var targets = document.querySelectorAll(".reveal, .progress-line");
    if (!targets.length) return;

    if (!("IntersectionObserver" in window)) {
      targets.forEach(function (el) {
        el.classList.add("is-visible");
      });
      return;
    }

    var observer = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          entry.target.classList.add("is-visible");
          observer.unobserve(entry.target);
        });
      },
      { threshold: 0.15, rootMargin: "0px 0px -40px 0px" }
    );

    targets.forEach(function (el) {
      observer.observe(el);
    });
  }

  /* -------------------------------------------------------------
     4. Statistics counters
     Counts up once, then stops permanently. No timers left running.
     ------------------------------------------------------------- */
  function initCounters() {
    var counters = document.querySelectorAll("[data-count-to]");
    if (!counters.length) return;

    function render(el, value) {
      var decimals = parseInt(el.dataset.decimals || "0", 10);
      var prefix = el.dataset.prefix || "";
      var suffix = el.dataset.suffix || "";
      var shown = decimals > 0 ? value.toFixed(decimals) : Math.round(value).toLocaleString("en-IN");
      el.textContent = prefix + shown + suffix;
    }

    function run(el) {
      var target = parseFloat(el.dataset.countTo);
      if (isNaN(target)) return;

      if (reducedMotion) {
        render(el, target);
        return;
      }

      var duration = 1200;
      var start = null;

      function step(timestamp) {
        if (start === null) start = timestamp;
        var progress = Math.min((timestamp - start) / duration, 1);
        // Ease-out so it slows down as it lands on the final number.
        var eased = 1 - Math.pow(1 - progress, 3);
        render(el, target * eased);
        if (progress < 1) requestAnimationFrame(step);
      }

      requestAnimationFrame(step);
    }

    if (!("IntersectionObserver" in window)) {
      counters.forEach(run);
      return;
    }

    var observer = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          run(entry.target);
          observer.unobserve(entry.target);
        });
      },
      { threshold: 0.4 }
    );

    counters.forEach(function (el) {
      observer.observe(el);
    });
  }

  /* -------------------------------------------------------------
     5. Quote form helpers
     ------------------------------------------------------------- */
  function initQuoteForm() {
    // Stop people picking a moving date in the past.
    var dateInput = document.querySelector('[data-min-today="true"]');
    if (dateInput) {
      var today = new Date();
      var iso = today.getFullYear() +
        "-" + String(today.getMonth() + 1).padStart(2, "0") +
        "-" + String(today.getDate()).padStart(2, "0");
      dateInput.min = iso;
    }

    // Any "Get Free Quotes" link jumps to the form and focuses it,
    // so keyboard and screen reader users land in the right place.
    document.addEventListener("click", function (event) {
      var trigger = event.target.closest('a[href="#get-quotes"]');
      if (!trigger) return;

      var firstField = document.getElementById("ServiceType");
      if (!firstField) return;

      window.setTimeout(function () {
        firstField.focus({ preventScroll: true });
      }, reducedMotion ? 0 : 500);
    });
  }
})();