(function () {
    "use strict";

    var wf = document.querySelector(".wf");
    if (!wf) return;

    var map = [
        ["sm-massbalance", 2],
        ["sm-digester", 3],
        ["sm-thermal", 4],
        ["sm-upgrading", 5],
        ["sm-digestate", 6]
    ];

    var sections = [];
    map.forEach(function (m) {
        var el = document.getElementById(m[0]);
        if (el) sections.push({ el: el, step: m[1] });
    });
    if (!sections.length) return;

    var pills = Array.prototype.slice.call(document.querySelectorAll(".sx-pill"));
    var pillTargets = pills.map(function (a) {
        return document.getElementById((a.getAttribute("href") || "").replace("#", ""));
    });

    function setPill(line) {
        var active = -1;
        pillTargets.forEach(function (el, i) {
            if (el && el.getBoundingClientRect().top <= line) active = i;
        });
        if (active < 0 && pills.length) active = 0;
        pills.forEach(function (a, i) {
            a.classList.toggle("is-active", i === active);
            if (i === active) a.setAttribute("aria-current", "true");
            else a.removeAttribute("aria-current");
        });
    }

    var rows = wf.querySelectorAll(".wf__row");
    var tag = wf.querySelector(".wf__tag");
    var CHECK = "M5 12l5 5 9-10";
    var total = rows.length;
    var shown = 0;
    var queued = false;

    function setStep(n) {
        if (n === shown) return;
        shown = n;

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
    }

    function compute() {
        queued = false;
        var line = window.innerHeight * 0.3;
        var step = sections[0].step;

        sections.forEach(function (s) {
            if (s.el.getBoundingClientRect().top <= line) step = s.step;
        });

        setStep(step);
        setPill(line);
    }

    function queue() {
        if (queued) return;
        queued = true;
        window.requestAnimationFrame(compute);
    }

    document.addEventListener("scroll", queue, true);
    window.addEventListener("resize", queue);
    window.addEventListener("hashchange", queue);
    window.addEventListener("load", queue);
    queue();
})();