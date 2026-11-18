// Small helpers for the account forms. Blazor can only focus an element it holds a reference to;
// the forms know the id of the first invalid field, so they move focus by id.
window.nmpForms = {
    focusById(id) {
        const element = document.getElementById(id);
        if (element) {
            element.focus();
        }
    },
};
