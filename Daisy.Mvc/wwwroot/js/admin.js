// Daisy admin: chart tooltips (hover and keyboard focus) and table toggles.
(function () {
  "use strict";

  const tip = document.querySelector("[data-tooltip]");

  function show(target, x, y) {
    if (!tip) return;
    tip.replaceChildren();
    const value = document.createElement("strong");
    value.textContent = target.dataset.tipValue;
    const label = document.createElement("span");
    label.textContent = target.dataset.tipLabel + (target.dataset.tipDetail ? `, ${target.dataset.tipDetail}` : "");
    tip.append(value, label);
    tip.hidden = false;
    const box = tip.getBoundingClientRect();
    const left = Math.min(window.innerWidth - box.width - 8, Math.max(8, x - box.width / 2));
    const top = y - box.height - 12 < 8 ? y + 16 : y - box.height - 12;
    tip.style.left = `${left}px`;
    tip.style.top = `${top}px`;
  }

  function hide() {
    if (tip) tip.hidden = true;
  }

  document.addEventListener("pointermove", (e) => {
    const target = e.target.closest?.("[data-tip-value]");
    if (target) show(target, e.clientX, e.clientY);
    else hide();
  });

  document.addEventListener("focusin", (e) => {
    const target = e.target.closest?.("[data-tip-value]");
    if (!target) return;
    const r = target.getBoundingClientRect();
    show(target, r.left + r.width / 2, r.top);
  });
  document.addEventListener("focusout", hide);
  window.addEventListener("scroll", hide, { passive: true });

  // Slug from a Vietnamese name: "Áo thun nam" -> "ao-thun-nam". Stops once the slug is edited by hand.
  const source = document.querySelector("[data-slug-source]");
  const target = document.querySelector("[data-slug-target]");
  if (source && target) {
    let manual = target.value.trim() !== "";
    const slugify = (text) => text.toLowerCase().replace(/đ/g, "d").normalize("NFD").replace(/[̀-ͯ]/g, "")
      .replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
    target.addEventListener("input", () => (manual = target.value.trim() !== ""));
    source.addEventListener("input", () => { if (!manual) target.value = slugify(source.value); });
  }

  document.querySelectorAll("[data-toggle-table]").forEach((button) => {
    const table = document.getElementById(button.dataset.toggleTable);
    button.setAttribute("aria-controls", table.id);
    button.setAttribute("aria-expanded", "false");
    button.addEventListener("click", () => {
      table.hidden = !table.hidden;
      button.setAttribute("aria-expanded", String(!table.hidden));
      button.textContent = table.hidden ? "Xem dạng bảng" : "Ẩn bảng";
    });
  });
})();
