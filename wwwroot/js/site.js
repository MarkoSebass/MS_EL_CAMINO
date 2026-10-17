// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener("DOMContentLoaded", () => {
	document.querySelectorAll("[data-uppercase]").forEach((input) => {
		const uppercaseValue = input.value.toUpperCase();
		input.value = uppercaseValue;

		input.addEventListener("input", () => {
			const selectionStart = input.selectionStart;
			const selectionEnd = input.selectionEnd;
			const uppercaseValue = input.value.toUpperCase();

			if (input.value === uppercaseValue) {
				return;
			}

			input.value = uppercaseValue;
			if (selectionStart !== null && selectionEnd !== null) {
				input.setSelectionRange(selectionStart, selectionEnd);
			}
		});
	});

	let pendingLogoutForm = null;
	const logoutModalElement = document.getElementById("logoutConfirmationModal");

	document.querySelectorAll("form[data-confirm-logout]").forEach((logoutForm) => {
		logoutForm.addEventListener("submit", (event) => {
			if (logoutForm.dataset.logoutConfirmed === "true") {
				delete logoutForm.dataset.logoutConfirmed;
				return;
			}

			event.preventDefault();
			pendingLogoutForm = logoutForm;
			bootstrap.Modal.getOrCreateInstance(logoutModalElement).show();
		});
	});

	document.getElementById("confirmLogoutButton")?.addEventListener("click", () => {
		if (!pendingLogoutForm) {
			return;
		}

		const logoutForm = pendingLogoutForm;
		pendingLogoutForm = null;
		logoutForm.dataset.logoutConfirmed = "true";
		logoutForm.requestSubmit();
	});

	document.querySelectorAll(".app-toast").forEach((toastElement) => {
		bootstrap.Toast.getOrCreateInstance(toastElement).show();
	});
});
