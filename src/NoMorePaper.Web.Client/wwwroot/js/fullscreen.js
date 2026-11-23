// Full screen for one element of a page (the viewer column). A small block of its own so the viewer code in
// js/viewer stays untouched. Browsers on the iPhone cannot show elements in full screen (only videos), so
// watch() reports whether it is possible at all and the page shows no button when it is not.
// While the element is in full screen everything around it is made inert: the Tab key would otherwise leave
// the full-screen element and land on controls the user cannot see.
window.nmpFullscreen = (function () {
    const watchers = new Map();

    function supported() {
        return document.fullscreenEnabled === true && typeof Element.prototype.requestFullscreen === 'function';
    }

    // Sets inert on every sibling along the path from the element up to <body>; returns what it changed.
    function isolate(element) {
        const changed = [];
        for (let node = element; node && node !== document.body; node = node.parentElement) {
            for (const sibling of node.parentElement ? node.parentElement.children : []) {
                if (sibling !== node && !sibling.inert) {
                    sibling.inert = true;
                    changed.push(sibling);
                }
            }
        }

        return changed;
    }

    function release(entry) {
        for (const element of entry.changed) {
            element.inert = false;
        }

        entry.changed = [];
    }

    return {
        watch(id, dotnet) {
            if (!supported()) {
                return false;
            }

            this.unwatch(id);
            const entry = { changed: [], handler: null };
            entry.handler = () => {
                const element = document.getElementById(id);
                const active = !!element && document.fullscreenElement === element;
                release(entry);
                if (active) {
                    entry.changed = isolate(element);
                }

                dotnet.invokeMethodAsync('OnFullscreenChanged', active);
            };
            document.addEventListener('fullscreenchange', entry.handler);
            watchers.set(id, entry);
            return true;
        },

        unwatch(id) {
            const entry = watchers.get(id);
            if (entry) {
                document.removeEventListener('fullscreenchange', entry.handler);
                release(entry);
                watchers.delete(id);
            }

            const element = document.getElementById(id);
            if (element && document.fullscreenElement === element) {
                document.exitFullscreen();
            }
        },

        // Returns the name of the error (e.g. NotAllowedError, TypeError) or null; the page logs and announces it.
        async toggle(id) {
            const element = document.getElementById(id);
            if (!element) {
                return null;
            }

            try {
                if (document.fullscreenElement) {
                    await document.exitFullscreen();
                } else {
                    await element.requestFullscreen();
                }

                return null;
            } catch (error) {
                return (error && error.name) || 'Error';
            }
        },
    };
})();
