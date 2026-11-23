// Full screen for one element of a page (the viewer column). A small block of its own so the viewer code in
// js/viewer stays untouched. Browsers on the iPhone cannot show elements in full screen (only videos), so
// watch() reports whether it is possible at all and the page shows no button when it is not.
window.nmpFullscreen = (function () {
    const watchers = new Map();

    function supported() {
        return document.fullscreenEnabled === true && typeof Element.prototype.requestFullscreen === 'function';
    }

    return {
        watch(id, dotnet) {
            if (!supported()) {
                return false;
            }

            this.unwatch(id);
            const handler = () => {
                const active = document.fullscreenElement === document.getElementById(id);
                dotnet.invokeMethodAsync('OnFullscreenChanged', active);
            };
            document.addEventListener('fullscreenchange', handler);
            watchers.set(id, handler);
            return true;
        },

        unwatch(id) {
            const handler = watchers.get(id);
            if (handler) {
                document.removeEventListener('fullscreenchange', handler);
                watchers.delete(id);
            }

            const element = document.getElementById(id);
            if (element && document.fullscreenElement === element) {
                document.exitFullscreen();
            }
        },

        async toggle(id) {
            const element = document.getElementById(id);
            if (!element) {
                return;
            }

            if (document.fullscreenElement) {
                await document.exitFullscreen();
            } else {
                await element.requestFullscreen();
            }
        },
    };
})();
