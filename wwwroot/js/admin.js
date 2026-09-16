/* ---------------------------------------------------------------
   ShiftingGuru admin - sidebar drawer only.
   Everything else in the admin panel is plain server-rendered HTML.
   --------------------------------------------------------------- */
(function () {
  "use strict";

  var toggle = document.getElementById("admin-menu-toggle");
  var sidebar = document.getElementById("admin-sidebar");
  var overlay = document.getElementById("admin-overlay");

  if (!toggle || !sidebar || !overlay) return;

  var desktop = window.matchMedia("(min-width: 1024px)");

  // On desktop the sidebar is a normal column, so the hidden attribute has
  // to come off or it stays invisible.
  function syncForViewport() {
    if (desktop.matches) {
      sidebar.hidden = false;
      overlay.hidden = true;
      sidebar.classList.remove("is-open");
      document.body.style.overflow = "";
      toggle.setAttribute("aria-expanded", "false");
    } else if (!sidebar.classList.contains("is-open")) {
      sidebar.hidden = true;
    }
  }

  function open() {
    overlay.hidden = false;
    sidebar.hidden = false;
    requestAnimationFrame(function () {
      sidebar.classList.add("is-open");
    });
    toggle.setAttribute("aria-expanded", "true");
    document.body.style.overflow = "hidden";
  }

  function close() {
    sidebar.classList.remove("is-open");
    toggle.setAttribute("aria-expanded", "false");
    document.body.style.overflow = "";

    window.setTimeout(function () {
      if (!desktop.matches) {
        sidebar.hidden = true;
        overlay.hidden = true;
      }
    }, 280);
  }

  toggle.addEventListener("click", open);
  overlay.addEventListener("click", close);

  sidebar.addEventListener("click", function (event) {
    if (event.target.closest("a") && !desktop.matches) close();
  });

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape" && sidebar.classList.contains("is-open")) close();
  });

  desktop.addEventListener("change", syncForViewport);
  syncForViewport();
})();