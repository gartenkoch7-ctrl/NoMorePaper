// Global keyboard shortcuts. "/" jumps to the search field, unless the user is typing somewhere.
// It checks event.key, not a key code: on a German keyboard "/" is Shift+7.
document.addEventListener('keydown', function (event) {
  if (event.key !== '/' || event.ctrlKey || event.altKey || event.metaKey) {
    return;
  }

  var target = event.target;
  if (target instanceof HTMLElement) {
    var tag = target.tagName;
    if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target.isContentEditable) {
      return;
    }
  }

  var input = document.getElementById('header-search-input');
  if (input) {
    event.preventDefault();
    input.focus();
    input.select();
  }
});
