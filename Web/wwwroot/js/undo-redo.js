(function (global) {
    'use strict';

    function resolve(el) {
        if (!el) return null;
        return typeof el === 'string' ? document.getElementById(el) : el;
    }

    function UndoRedo(options) {
        options = options || {};

        var limit = options.limit || 100;
        var getState = options.getState;
        var setState = options.setState;
        var onChange = options.onChange;
        var isActive = options.isActive;
        var undoBtn = resolve(options.undoButton);
        var redoBtn = resolve(options.redoButton);

        var undoStack = [];
        var redoStack = [];
        var current = null;
        var busy = false;

        function serialize() {
            return JSON.stringify(getState());
        }

        function notify() {
            var canUndo = undoStack.length > 0;
            var canRedo = redoStack.length > 0;

            if (undoBtn) {
                undoBtn.disabled = !canUndo;
                undoBtn.title = 'Undo (Ctrl+Z)';
            }
            if (redoBtn) {
                redoBtn.disabled = !canRedo;
                redoBtn.title = 'Redo (Ctrl+Y)';
            }
            if (typeof onChange === 'function') {
                onChange({ canUndo: canUndo, canRedo: canRedo });
            }
        }

        function apply(snapshot) {
            busy = true;
            try {
                setState(JSON.parse(snapshot));
            } finally {
                busy = false;
            }
            notify();
        }

        function reset() {
            undoStack = [];
            redoStack = [];
            try {
                current = serialize();
            } catch (e) {
                current = null;
            }
            notify();
        }

        function commit() {
            if (busy) return;
            var snapshot = serialize();
            if (snapshot === current) return;

            if (current !== null) undoStack.push(current);
            if (undoStack.length > limit) undoStack.shift();

            current = snapshot;
            redoStack = [];
            notify();
        }

        function undo() {
            if (!undoStack.length) return;
            redoStack.push(current);
            current = undoStack.pop();
            apply(current);
        }

        function redo() {
            if (!redoStack.length) return;
            undoStack.push(current);
            current = redoStack.pop();
            apply(current);
        }

        function onKey(e) {
            if (!(e.ctrlKey || e.metaKey)) return;
            if (typeof isActive === 'function' && !isActive()) return;

            var t = e.target;
            var tag = t && t.tagName ? t.tagName.toUpperCase() : '';
            if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || (t && t.isContentEditable)) return;

            var k = (e.key || '').toLowerCase();
            if (k === 'z' && !e.shiftKey) {
                e.preventDefault();
                undo();
            } else if (k === 'y' || (k === 'z' && e.shiftKey)) {
                e.preventDefault();
                redo();
            }
        }

        function onUndoClick(e) {
            e.preventDefault();
            undo();
        }

        function onRedoClick(e) {
            e.preventDefault();
            redo();
        }

        if (options.keys !== false) document.addEventListener('keydown', onKey);
        if (undoBtn) undoBtn.addEventListener('click', onUndoClick);
        if (redoBtn) redoBtn.addEventListener('click', onRedoClick);

        function destroy() {
            document.removeEventListener('keydown', onKey);
            if (undoBtn) undoBtn.removeEventListener('click', onUndoClick);
            if (redoBtn) redoBtn.removeEventListener('click', onRedoClick);
        }

        reset();

        return {
            commit: commit,
            undo: undo,
            redo: redo,
            reset: reset,
            destroy: destroy,
            isBusy: function () { return busy; },
            canUndo: function () { return undoStack.length > 0; },
            canRedo: function () { return redoStack.length > 0; }
        };
    }

    global.UndoRedo = UndoRedo;
})(window);
