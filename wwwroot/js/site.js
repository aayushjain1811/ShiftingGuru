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
    initJourney();
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
     One shared observer for every reveal class on the site. Each
     element is unobserved after it fires, so nothing keeps running
     in the background.
     ------------------------------------------------------------- */
  function initReveal() {
    var targets = document.querySelectorAll(
      ".reveal, .progress-line, .reveal-up, .reveal-left, .reveal-right, " +
      ".reveal-scale, .reveal-blur, .reveal-clip, .timeline-rail, " +
      ".timeline-rail-v, .route-road, .photo-drift"
    );
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
     4. The move journey
     Plays itself, like a short film: every few seconds the next stage
     fades in, the line fills and the truck moves along. It only runs
     while the section is on screen, so nothing ticks in the background,
     and it pauses on hover or keyboard focus. Clicking a step jumps
     straight to it.
     ------------------------------------------------------------- */
  function initJourney() {
    var journey = document.getElementById("journey");
    if (!journey) return;

    var steps = journey.querySelectorAll(".journey-step");
    var stages = journey.querySelectorAll(".stage");
    var fill = journey.querySelector(".journey-fill");
    var truck = journey.querySelector(".journey-truck");
    if (!steps.length) return;

    var current = 0;
    var timer = null;
    var HOLD = 3200;   // milliseconds each stage stays on screen

    function activate(index) {
      current = index;

      steps.forEach(function (step, i) {
        step.classList.toggle("is-active", i === index);
        step.classList.toggle("is-done", i < index);
      });

      stages.forEach(function (stage, i) {
        stage.classList.toggle("is-active", i === index);
      });

      var percent = ((index + 1) / steps.length) * 100;
      if (fill) fill.style.width = percent + "%";
      if (truck) truck.style.left = percent + "%";
    }

    function play() {
      if (timer || reducedMotion) return;
      timer = window.setInterval(function () {
        activate((current + 1) % steps.length);
      }, HOLD);
    }

    function pause() {
      window.clearInterval(timer);
      timer = null;
    }

    // Clicking or tabbing to a step takes over from the autoplay.
    steps.forEach(function (step) {
      step.addEventListener("click", function () {
        activate(Number(step.dataset.step));
        pause();
      });

      step.addEventListener("focus", pause);
    });

    journey.addEventListener("mouseenter", pause);
    journey.addEventListener("mouseleave", play);

    activate(0);

    // Only run while the section is actually visible.
    if (!("IntersectionObserver" in window)) {
      play();
      return;
    }

    new IntersectionObserver(
      function (entries) {
        entries[0].isIntersecting ? play() : pause();
      },
      { threshold: 0.25 }
    ).observe(journey);
  }

  /* -------------------------------------------------------------
     5. Statistics counters
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
     6. Quote form helpers
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
    // The hero field is "hero-service"; the full form on /quote uses
    // "ServiceType", so try both.
    document.addEventListener("click", function (event) {
      var trigger = event.target.closest('a[href="#get-quotes"]');
      if (!trigger) return;

      var firstField =
        document.getElementById("hero-service") || document.getElementById("ServiceType");
      if (!firstField) return;

      window.setTimeout(function () {
        firstField.focus({ preventScroll: true });
      }, reducedMotion ? 0 : 500);
    });
  }
})();