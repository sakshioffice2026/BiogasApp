(function () {
    "use strict";

    var layout = document.querySelector(".wf-layout");
    if (!layout) return;

    var page = (layout.getAttribute("data-wf-page") || "").toLowerCase();
    var wf = layout.querySelector(".wf");
    var rows = wf ? wf.querySelectorAll(".wf__row") : [];
    var tag = wf ? wf.querySelector(".wf__tag") : null;
    var card = document.querySelector("[data-wf-next]");
    var nextLabel = card ? card.querySelector("[data-wf-next-label]") : null;
    var nextHint = card ? card.querySelector("[data-wf-next-hint]") : null;
    var nextLink = card ? card.querySelector("[data-wf-next-link]") : null;
    var submitBtn = card ? card.querySelector("[data-wf-submit]") : null;
    var proposalUrl = card ? card.getAttribute("data-proposal-url") : "";
    var CHECK = "M5 12l5 5 9-10";
    var total = rows.length || 7;

    function setStep(n) {
        rows.forEach(function (row, i) {
            var idx = i + 1;
            var state = idx < n ? "is-done" : idx === n ? "is-current" : "is-upcoming";

            row.classList.remove("is-done", "is-current", "is-upcoming");
            row.classList.add(state);

            if (idx === n) {
                row.setAttribute("aria-current", "step");
            } else {
                row.removeAttribute("aria-current");
            }

            row.querySelectorAll("path[data-icon]").forEach(function (p) {
                p.setAttribute("d", idx < n ? CHECK : p.getAttribute("data-icon"));
            });
        });

        if (tag) tag.textContent = "Current: step " + n + " of " + total;
        if (nextHint) nextHint.textContent = "Step " + n + " of " + total;
    }

    /* ---------- Mixer: top-right button submits the mixer form ---------- */
    if (submitBtn) {
        submitBtn.addEventListener("click", function () {
            var form = document.querySelector(submitBtn.getAttribute("data-wf-submit"));
            if (!form) return;
            var real = form.querySelector("[type=submit]");
            if (real) {
                real.click();
            } else if (form.requestSubmit) {
                form.requestSubmit();
            } else {
                form.submit();
            }
        });
    }

    /* ---------- Wizard: simulation dashboard only ---------- */
    if (page !== "simulation/dashboard") return;

    var defs = [
        { id: "sm-massbalance", step: 2, label: "Mass balance and pre-treatment" },
        { id: "sm-digester", step: 3, label: "Digester performance" },
        { id: "sm-thermal", step: 4, label: "Thermal, mixing and shell" },
        { id: "sm-upgrading", step: 5, label: "Upgrading and compression" },
        { id: "sm-digestate", step: 6, label: "Digestate, civils and dispatch" }
    ];

    var panels = [];
    defs.forEach(function (d) {
        var el = document.getElementById(d.id);
        if (el) panels.push({ id: d.id, step: d.step, label: d.label, el: el, extra: [] });
    });
    if (panels.length < 2) return;

    var eqTitle = document.getElementById("sm-eq-title");
    var eqSection = eqTitle ? eqTitle.closest("section") : null;
    if (eqSection) panels[panels.length - 1].extra.push(eqSection);

    layout.classList.add("sm-wizard");

    var current = -1;

    function indexOfId(id) {
        for (var i = 0; i < panels.length; i++) {
            if (panels[i].id === id) return i;
        }
        return -1;
    }

    function updateNext(index) {
        if (!nextLabel) return;
        var isLast = index >= panels.length - 1;

        if (!isLast) {
            nextLabel.textContent = "Next: " + panels[index + 1].label;
            if (nextLink) nextLink.setAttribute("href", "#" + panels[index + 1].id);
        } else if (proposalUrl) {
            nextLabel.textContent = "Build proposal";
            if (nextLink) nextLink.setAttribute("href", proposalUrl);
        } else if (card) {
            card.style.display = "none";
        }
    }

    function show(index, scroll) {
        if (index < 0 || index >= panels.length) return;
        current = index;

        panels.forEach(function (p, i) {
            var visible = i === index;
            p.el.classList.toggle("sm-is-hidden", !visible);
            p.el.classList.toggle("sm-wizard-active", visible);
            p.extra.forEach(function (x) {
                x.classList.toggle("sm-is-hidden", !visible);
            });
        });

        setStep(panels[index].step);
        updateNext(index);

        if (window.history && window.history.replaceState) {
            window.history.replaceState(null, "", "#" + panels[index].id);
        }

        if (scroll) {
            panels[index].el.scrollIntoView({ behavior: "smooth", block: "start" });
        }
    }

    /* Stepper rows: click to open that step */
    rows.forEach(function (row, i) {
        var idx = i + 1;
        var panelIndex = -1;

        panels.forEach(function (p, k) {
            if (p.step === idx) panelIndex = k;
        });

        var goesToProposal = idx === total && proposalUrl;
        if (panelIndex < 0 && !goesToProposal) return;

        row.setAttribute("data-clickable", "");
        row.setAttribute("tabindex", "0");
        row.setAttribute("role", "link");

        function go() {
            if (goesToProposal) {
                window.location.href = proposalUrl;
            } else {
                show(panelIndex, true);
            }
        }

        row.addEventListener("click", go);
        row.addEventListener("keydown", function (e) {
            if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                go();
            }
        });
    });

    /* Any in-page "#sm-..." link (top-right next button, "Next: ..." buttons in cards) */
    document.addEventListener("click", function (e) {
        var a = e.target.closest ? e.target.closest("a[href^='#sm-']") : null;
        if (!a) return;
        var idx = indexOfId(a.getAttribute("href").slice(1));
        if (idx < 0) return;
        e.preventDefault();
        show(idx, true);
    });

    window.addEventListener("hashchange", function () {
        var idx = indexOfId(window.location.hash.slice(1));
        if (idx >= 0 && idx !== current) show(idx, true);
    });

    var start = indexOfId(window.location.hash.slice(1));
    show(start >= 0 ? start : 0, false);
})();
