// Tab strips (Design/TabList.razor): arrow keys, Home and End move between tabs, handled in Blazor.
// Blazor cannot cancel single keys, so this stops the browser from also scrolling the page.
document.addEventListener('keydown', function (event) {
  var target = event.target;
  if (!(target instanceof HTMLElement) || target.getAttribute('role') !== 'tab') {
    return;
  }

  if (event.key === 'ArrowLeft' || event.key === 'ArrowRight' || event.key === 'Home' || event.key === 'End') {
    event.preventDefault();
  }
});
