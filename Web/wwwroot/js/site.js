(function () {
    'use strict';

    function debounce(fn, wait) {
        var timer;
        return function () {
            window.clearTimeout(timer);
            timer = window.setTimeout(fn, wait);
        };
    }

    function initTopbar() {
        var bar = document.querySelector('.bp-topbar');
        if (!bar) return;

        function onScroll() {
            bar.classList.toggle('is-scrolled', window.scrollY > 4);
        }

        window.addEventListener('scroll', onScroll, { passive: true });
        onScroll();
    }

    function initPipeline() {
        var list = document.querySelector('.bp-rail__list');
        if (!list) return;

        var nodes = list.querySelectorAll('.bp-node');
        if (!nodes.length) return;

        var active = list.querySelector('.bp-node.is-active');

        function dotCenter(node, horizontal, listRect) {
            var rect = node.querySelector('.bp-node__dot').getBoundingClientRect();
            return horizontal
                ? rect.left - listRect.left + rect.width / 2
                : rect.top - listRect.top + rect.height / 2;
        }

        function measure() {
            var horizontal = window.getComputedStyle(list).flexDirection === 'row';
            var listRect = list.getBoundingClientRect();
            var first = dotCenter(nodes[0], horizontal, listRect);
            var last = dotCenter(nodes[nodes.length - 1], horizontal, listRect);
            var current = active ? dotCenter(active, horizontal, listRect) : first;

            return {
                start: first,
                end: last,
                fill: Math.max(0, current - first)
            };
        }

        function apply(values, animate) {
            list.style.setProperty('--bp-start', values.start + 'px');
            list.style.setProperty('--bp-end', values.end + 'px');

            if (!animate) {
                list.style.setProperty('--bp-fill', values.fill + 'px');
                return;
            }

            list.style.setProperty('--bp-fill', '0px');
            window.requestAnimationFrame(function () {
                window.requestAnimationFrame(function () {
                    list.style.setProperty('--bp-fill', values.fill + 'px');
                });
            });
        }

        apply(measure(), true);

        window.addEventListener('resize', debounce(function () {
            apply(measure(), false);
        }, 120));

        if (document.fonts && document.fonts.ready) {
            document.fonts.ready.then(function () {
                apply(measure(), false);
            });
        }
    }

    function initFlash() {
        var items = document.querySelectorAll('[data-bp-autohide]');
        Array.prototype.forEach.call(items, function (el) {
            window.setTimeout(function () {
                if (!document.body.contains(el)) return;
                if (window.bootstrap && window.bootstrap.Alert) {
                    window.bootstrap.Alert.getOrCreateInstance(el).close();
                } else {
                    el.remove();
                }
            }, 6000);
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        initTopbar();
        initPipeline();
        initFlash();
    });
})();