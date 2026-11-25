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

const homePage = document.querySelector(".home-page");
const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

if (homePage && !prefersReducedMotion && "IntersectionObserver" in window) {
    const revealTargets = homePage.querySelectorAll(
        ".hero-copy, .road-card, .manifesto-wrapper, .value-card, .module-card, .timeline-step-card, .cta-banner"
    );

    revealTargets.forEach((target, index) => {
        target.dataset.reveal = "";
        target.style.setProperty("--reveal-delay", `${(index % 4) * 75}ms`);
    });

    const revealObserver = new IntersectionObserver((entries, observer) => {
        entries.forEach((entry) => {
            if (entry.isIntersecting) {
                entry.target.classList.add("is-revealed");
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.12, rootMargin: "0px 0px -24px 0px" });

    revealTargets.forEach((target) => revealObserver.observe(target));
}

const roadCard = homePage?.querySelector(".road-card");
const supportsHover = window.matchMedia("(hover: hover) and (pointer: fine)").matches;

if (roadCard && supportsHover && !prefersReducedMotion) {
    roadCard.classList.add("road-card--interactive");
    roadCard.addEventListener("pointermove", (event) => {
        const bounds = roadCard.getBoundingClientRect();
        const horizontalPosition = (event.clientX - bounds.left) / bounds.width - 0.5;
        const verticalPosition = (event.clientY - bounds.top) / bounds.height - 0.5;

        roadCard.style.setProperty("--road-tilt-x", `${horizontalPosition * 5}deg`);
        roadCard.style.setProperty("--road-tilt-y", `${verticalPosition * -5}deg`);
    });

    roadCard.addEventListener("pointerleave", () => {
        roadCard.style.setProperty("--road-tilt-x", "0deg");
        roadCard.style.setProperty("--road-tilt-y", "0deg");
    });
}
