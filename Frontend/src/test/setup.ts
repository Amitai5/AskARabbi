import '@testing-library/jest-dom/vitest'

// jsdom does not implement the native modal-dialog lifecycle.
HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', '') }
HTMLDialogElement.prototype.close = function () { this.removeAttribute('open') }
