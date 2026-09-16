/* ---------------------------------------------------------------
   ShiftingGuru - review-form.js
   Hover and keyboard feedback for the star rating. The rating itself
   is a set of real radio inputs, so the form works and validates
   with JavaScript switched off.
   --------------------------------------------------------------- */
(function () {
  "use strict";

  var group = document.querySelector("[data-star-group]");
  if (!group) return;

  var labels = group.querySelectorAll("[data-star]");
  var output = document.querySelector("[data-star-output]");

  var words = ["", "Poor", "Fair", "Good", "Very good", "Excellent"];

  function paint(upTo) {
    labels.forEach(function (label) {
      var value = Number(label.dataset.star);
      label.classList.toggle("is-lit", value <= upTo);
    });
  }

  function selected() {
    var checked = group.querySelector("input[type=radio]:checked");
    return checked ? Number(checked.value) : 0;
  }

  function describe(value) {
    if (output) output.textContent = value > 0 ? words[value] : "";
  }

  // One listener on the group rather than one per star.
  group.addEventListener("mouseover", function (event) {
    var label = event.target.closest("[data-star]");
    if (label) paint(Number(label.dataset.star));
  });

  group.addEventListener("mouseleave", function () {
    paint(selected());
    describe(selected());
  });

  group.addEventListener("change", function () {
    paint(selected());
    describe(selected());
  });

  // Arrow keys move between radios natively; this just keeps the paint in sync.
  group.addEventListener("focusin", function () {
    paint(selected());
  });

  paint(selected());
  describe(selected());
})();