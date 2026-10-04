// Daisy storefront behaviour. Progressive enhancement: every form still works without JavaScript.
(function () {
  "use strict";

  const pad = (n) => String(n).padStart(2, "0");

  // ---- Countdown to a deadline (flash sale, VietQR payment deadline) ----------------------
  function startCountdowns() {
    document.querySelectorAll("[data-deadline]").forEach((root) => {
      const deadline = new Date(root.dataset.deadline).getTime();
      const out = root.querySelector("[data-clock]") || root;
      const tick = () => {
        const left = Math.max(0, deadline - Date.now());
        const h = Math.floor(left / 3600000);
        const m = Math.floor((left % 3600000) / 60000);
        const s = Math.floor((left % 60000) / 1000);
        out.textContent = h > 0 ? `${pad(h)}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`;
        if (left === 0) {
          clearInterval(timer);
          root.dispatchEvent(new CustomEvent("deadline", { bubbles: true }));
        }
      };
      const timer = setInterval(tick, 1000);
      tick();
    });
  }

  // ---- Product detail: size / color picker updates variant, price, stock and gallery -----
  function startVariantPicker() {
    const form = document.querySelector("[data-variant-form]");
    if (!form) return;
    const variants = JSON.parse(form.querySelector("[data-variants]").textContent);
    const images = JSON.parse(form.querySelector("[data-images]").textContent);
    const idInput = form.querySelector("[data-variant-id]");
    const qty = form.querySelector("input[name=quantity]");
    const price = document.querySelector("[data-price]");
    const stock = form.querySelector("[data-stock]");
    const colorLabel = form.querySelector("[data-color-label]");
    const addButtons = form.querySelectorAll("[data-add]");
    let size = form.querySelector("[data-size][aria-pressed=true]")?.dataset.size ?? null;
    let color = form.querySelector("[data-color][aria-pressed=true]")?.dataset.color ?? null;

    const find = () => variants.find((v) => (v.size ?? null) === size && (v.color ?? null) === color);

    function render() {
      form.querySelectorAll("[data-size]").forEach((b) => {
        b.setAttribute("aria-pressed", String(b.dataset.size === size));
        const any = variants.some((v) => v.size === b.dataset.size && (color === null || v.color === color) && v.stock > 0);
        b.disabled = !any;
      });
      form.querySelectorAll("[data-color]").forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.color === color)));
      if (colorLabel) colorLabel.textContent = color ?? "";

      const v = find();
      idInput.value = v ? v.id : 0;
      addButtons.forEach((b) => (b.disabled = !v || v.stock <= 0));
      if (!v) {
        stock.textContent = "Chọn size và màu.";
        stock.className = "stock-note";
        return;
      }
      price.textContent = v.priceText;
      qty.max = Math.max(1, v.stock);
      if (Number(qty.value) > v.stock) qty.value = Math.max(1, v.stock);
      if (v.stock <= 0) {
        stock.textContent = "Hết hàng";
        stock.className = "stock-note stock-note--out";
      } else if (v.low) {
        stock.textContent = `Chỉ còn ${v.stock} sản phẩm`;
        stock.className = "stock-note stock-note--low";
      } else {
        stock.textContent = "Còn hàng";
        stock.className = "stock-note";
      }
      showImageFor(color);
    }

    function showImageFor(c) {
      const match = images.find((i) => i.color === c) || null;
      if (match) { swapMain(match.url); return; }
      // No photo for this color: tint the placeholder with the garment color instead.
      const ph = document.querySelector("[data-gallery-main] .ph");
      const v = variants.find((x) => x.color === c);
      if (ph && v) { ph.style.setProperty("--swatch", v.swatch); ph.classList.toggle("ph--dark", v.dark); }
    }

    form.addEventListener("click", (e) => {
      const s = e.target.closest("[data-size]");
      const c = e.target.closest("[data-color]");
      if (s && !s.disabled) { size = s.dataset.size; render(); }
      if (c) {
        color = c.dataset.color;
        if (!find()) {
          const firstInColor = variants.find((v) => v.color === color && v.stock > 0) || variants.find((v) => v.color === color);
          if (firstInColor) size = firstInColor.size ?? null;
        }
        render();
      }
    });
    render();
  }

  // ---- Gallery -------------------------------------------------------------------------------
  function swapMain(url) {
    const main = document.querySelector("[data-gallery-main]");
    if (!main || !url) return;
    main.innerHTML = "";
    const img = document.createElement("img");
    img.src = url;
    img.alt = document.querySelector("h1")?.textContent ?? "";
    main.appendChild(img);
    document.querySelectorAll("[data-thumb]").forEach((t) => t.setAttribute("aria-current", String(t.dataset.thumb === url)));
  }

  function startGallery() {
    document.querySelectorAll("[data-thumb]").forEach((t) => t.addEventListener("click", () => swapMain(t.dataset.thumb)));
  }

  // ---- Quantity steppers ------------------------------------------------------------------------
  function startSteppers() {
    document.querySelectorAll("[data-qty]").forEach((box) => {
      const input = box.querySelector("input");
      box.querySelectorAll("[data-step]").forEach((b) =>
        b.addEventListener("click", () => {
          const max = Number(input.max || 999);
          const next = Math.min(max, Math.max(1, Number(input.value || 1) + Number(b.dataset.step)));
          if (next !== Number(input.value)) {
            input.value = next;
            input.dispatchEvent(new Event("change", { bubbles: true }));
          }
        }));
    });
  }

  // ---- Forms that submit themselves when a control changes (cart quantity / selection) ---------
  function startAutoSubmit() {
    document.querySelectorAll("form[data-autosubmit]").forEach((form) => {
      let timer;
      form.addEventListener("change", () => {
        clearTimeout(timer);
        timer = setTimeout(() => form.requestSubmit ? form.requestSubmit() : form.submit(), 350);
      });
      form.querySelectorAll("[data-hide-with-js]").forEach((el) => (el.hidden = true));
    });
  }

  // ---- VietQR: poll the order until it is paid (UC-35) -------------------------------------------
  function startPaymentPolling() {
    const root = document.querySelector("[data-poll-url]");
    if (!root) return;
    const url = root.dataset.pollUrl;
    const done = root.dataset.doneUrl;
    const stopAt = new Date(root.dataset.stopAt).getTime();
    const status = root.querySelector("[data-pay-status]");
    const poll = async () => {
      try {
        const res = await fetch(url, { headers: { Accept: "application/json" }, credentials: "same-origin" });
        if (res.ok) {
          const data = await res.json();
          if (data.paymentStatus === "Paid" || data.orderStatus === "Canceled") {
            if (status) status.textContent = data.paymentStatus === "Paid" ? "Đã nhận thanh toán. Đang chuyển đến đơn hàng…" : "Đơn hàng đã bị hủy.";
            window.location.assign(done);
            return;
          }
        }
      } catch { /* network hiccup: try again on the next tick */ }
      if (Date.now() < stopAt) setTimeout(poll, 4000);
      else if (status) status.textContent = "Hết thời gian chờ. Mở lại đơn hàng để xem trạng thái mới nhất.";
    };
    setTimeout(poll, 3000);
    root.addEventListener("deadline", () => { if (status) status.textContent = "Đã hết thời gian thanh toán. Chuyển khoản vừa thực hiện vẫn được ghi nhận trong vài phút."; });
  }

  // ---- Copy to clipboard ----------------------------------------------------------------------------
  function startCopy() {
    document.querySelectorAll("[data-copy]").forEach((b) =>
      b.addEventListener("click", async () => {
        try {
          await navigator.clipboard.writeText(b.dataset.copy);
          const old = b.textContent;
          b.textContent = "Đã chép";
          setTimeout(() => (b.textContent = old), 1500);
        } catch { /* clipboard not available */ }
      }));
  }

  document.addEventListener("DOMContentLoaded", () => {
    startCountdowns();
    startVariantPicker();
    startGallery();
    startSteppers();
    startAutoSubmit();
    startPaymentPolling();
    startCopy();
  });
})();
