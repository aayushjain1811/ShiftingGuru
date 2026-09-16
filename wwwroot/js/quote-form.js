/* ---------------------------------------------------------------
   ShiftingGuru - quote-form.js
   Live total on the vendor quote form. Convenience only: the server
   recalculates TotalAmount from the individual charges and ignores
   anything the browser claims the total is.
   --------------------------------------------------------------- */
(function () {
  "use strict";

  var form = document.getElementById("quote-form");
  if (!form) return;

  var output = form.querySelector("[data-quote-total]");
  var fields = form.querySelectorAll("[data-price]");
  if (!output || !fields.length) return;

  var formatter = new Intl.NumberFormat("en-IN", {
    style: "currency",
    currency: "INR",
    minimumFractionDigits: 2
  });

  function recalculate() {
    var total = 0;

    fields.forEach(function (field) {
      var value = parseFloat(field.value);
      if (!isNaN(value) && value > 0) total += value;
    });

    output.textContent = formatter.format(total);
  }

  // One listener on the form rather than one per input.
  form.addEventListener("input", function (event) {
    if (event.target.matches("[data-price]")) recalculate();
  });

  recalculate();
})();