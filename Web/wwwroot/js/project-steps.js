(function () {
    "use strict";

    var form = document.querySelector("[data-pj-wizard]");
    if (!form) return;

    var panels = Array.prototype.slice.call(form.querySelectorAll("[data-pj-step]"));
    if (panels.length < 2) return;

    var stepbar = form.querySelector("[data-pj-stepbar]");
    var items = form.querySelectorAll("[data-pj-item]");
    var backBtn = form.querySelector("[data-pj-back]");
    var nextBtn = form.querySelector("[data-pj-next]");
    var submitBtn = form.querySelector("[data-pj-submit]");
    var total = panels.length;
    var current = 1;

    if (stepbar) stepbar.hidden = false;

    // Hidden panels must still be validated by jQuery Validate.
    function configureValidator() {
        var $ = window.jQuery;
        if (!$ || !$.validator) return;
        var validator = $(form).validate();
        validator.settings.ignore = "";
    }

    function toNumber(selector) {
        var el = form.querySelector(selector);
        if (!el) return NaN;
        return parseFloat(String(el.value).replace(",", "."));
    }

    function panelOf(n) {
        return panels[n - 1];
    }

    function showStepError(panel, message) {
        var box = panel.querySelector("[data-pj-step-error]");
        if (!box) return;
        box.textContent = message || "";
        box.hidden = !message;
    }

    function validateStep(n) {
        var panel = panelOf(n);
        var ok = true;
        var $ = window.jQuery;

        if ($ && $.validator) {
            Array.prototype.forEach.call(
                panel.querySelectorAll("input, select, textarea"),
                function (el) {
                    if (el.type === "hidden") return;
                    if (!$(el).valid()) ok = false;
                });
        }

        showStepError(panel, "");

        if (n === 1) {
            var length = toNumber("[data-plot-length]");
            var width = toNumber("[data-plot-width]");
            if (!isNaN(length) && !isNaN(width) && length * width < 2500) {
                showStepError(panel, "Plot area must be at least 2,500 m\u00B2.");
                ok = false;
            }
        }

        if (n === 2) {
            var min = toNumber("[data-temp-min]");
            var max = toNumber("[data-temp-max]");
            if (!isNaN(min) && !isNaN(max) && min >= max) {
                showStepError(panel, "Minimum ambient temperature must be lower than the maximum.");
                ok = false;
            }
        }

        return ok;
    }

    function show(n, scroll) {
        current = Math.min(Math.max(n, 1), total);

        panels.forEach(function (p, i) {
            p.hidden = i !== current - 1;
        });

        Array.prototype.forEach.call(items, function (li) {
            var i = parseInt(li.getAttribute("data-pj-item"), 10);
            li.classList.toggle("is-current", i === current);
            li.classList.toggle("is-done", i < current);
            li.classList.toggle("is-upcoming", i > current);
            if (i === current) {
                li.setAttribute("aria-current", "step");
            } else {
                li.removeAttribute("aria-current");
            }
        });

        if (backBtn) backBtn.hidden = current === 1;
        if (nextBtn) nextBtn.hidden = current === total;
        if (submitBtn) submitBtn.hidden = current !== total;

        if (scroll && stepbar) {
            stepbar.scrollIntoView({ behavior: "smooth", block: "start" });
        }
    }

    function goTo(n) {
        if (n > current) {
            for (var s = current; s < n; s++) {
                if (!validateStep(s)) {
                    show(s, true);
                    return;
                }
            }
        }
        show(n, true);
    }

    form.addEventListener("click", function (e) {
        var target = e.target;
        if (!target || !target.closest) return;

        var go = target.closest("[data-pj-goto]");
        if (go) {
            goTo(parseInt(go.getAttribute("data-pj-goto"), 10));
            return;
        }

        if (target.closest("[data-pj-next]")) {
            goTo(current + 1);
            return;
        }

        if (target.closest("[data-pj-back]")) {
            show(current - 1, true);
        }
    });

    // Enter inside a text field moves to the next step instead of submitting early.
    form.addEventListener("keydown", function (e) {
        if (e.key !== "Enter") return;
        var tag = e.target && e.target.tagName;
        if (tag !== "INPUT" || e.target.type === "submit" || e.target.type === "radio") return;
        if (current < total) {
            e.preventDefault();
            goTo(current + 1);
        }
    });

    // Final check of every step before the form is posted.
    form.addEventListener("submit", function (e) {
        for (var n = 1; n <= total; n++) {
            if (!validateStep(n)) {
                e.preventDefault();
                show(n, true);
                return;
            }
        }
    });

    // Re-open on the first step that has a server-side error.
    var startAt = 1;
    for (var i = 0; i < panels.length; i++) {
        if (panels[i].querySelector(".input-validation-error, .field-validation-error:not(:empty)")) {
            startAt = i + 1;
            break;
        }
    }

    configureValidator();
    show(startAt, false);
})();
