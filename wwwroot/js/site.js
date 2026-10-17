document.addEventListener("show.bs.modal", (event) => {
    const modal = event.target;
    if (!(modal instanceof HTMLElement) || modal.id !== "access-modal") {
        return;
    }

    const trigger = event.relatedTarget;
    const moduleName = trigger instanceof HTMLElement ? trigger.dataset.module : "";
    const moduleLabel = modal.querySelector("#access-modal-module");
    if (moduleLabel) {
        moduleLabel.textContent = moduleName ? `DESTINO · ${moduleName.toLocaleUpperCase("es")}` : "";
    }
});

document.querySelectorAll("#main-navigation .module-link").forEach((link) => {
    link.addEventListener("click", () => {
        const navigation = document.getElementById("main-navigation");
        if (navigation?.classList.contains("show")) {
            bootstrap.Collapse.getOrCreateInstance(navigation).hide();
        }
    });
});
