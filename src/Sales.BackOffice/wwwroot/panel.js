// Sales' back office panel: the hotel's nightly rates. Part of Sales,
// deployed with the website.

export const title = "Sales: nightly rates";

const styles = document.createElement("link");
styles.rel = "stylesheet";
styles.href = new URL("panel.css", import.meta.url);
document.head.append(styles);

const pad = (n) => String(n).padStart(2, "0");
const iso = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const addDays = (d, days) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + days);
const euros = new Intl.NumberFormat(undefined, { style: "currency", currency: "EUR", maximumFractionDigits: 0 });
const day = new Intl.DateTimeFormat(undefined, { weekday: "short", day: "numeric", month: "short" });

export function create(element) {
  element.innerHTML = `
    <div class="fields">
      <label>Nightly rate for the next year, in euros <input type="number" name="nightlyRate" min="1" step="1" required></label>
    </div>`;
  return () => ({ nightlyRate: Number(element.querySelector('[name="nightlyRate"]').value) });
}

export function edit(element, hotel, save) {
  const rates = hotel.nightlyRates ?? [];
  const list = document.createElement("ul");
  list.className = "sales-rates";
  list.innerHTML = rates.length === 0
    ? `<li>No rates in the next two weeks</li>`
    : rates.map((r) => `<li>${day.format(new Date(`${r.night}T00:00`))}<strong>${euros.format(r.rate)}</strong></li>`).join("");

  const today = new Date();
  const form = document.createElement("form");
  form.innerHTML = `
    <div class="sales-range">
      <label>From <input type="date" name="from" required value="${iso(today)}" min="${iso(today)}"></label>
      <label>Until, excluded <input type="date" name="until" required value="${iso(addDays(today, 7))}"></label>
      <label>Nightly rate, in euros <input type="number" name="rate" min="1" step="1" required></label>
    </div>
    <div class="actions"><span class="saved" role="status"></span> <button type="submit">Set rates</button></div>`;
  element.append(list, form);

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    const field = (name) => form.querySelector(`[name="${name}"]`).value;
    const saved = form.querySelector(".saved");
    saved.textContent = "Saving…";
    const error = await save({ from: field("from"), until: field("until"), rate: Number(field("rate")) });
    saved.dataset.kind = error ? "error" : "info";
    saved.textContent = error ?? "Saved. Sales applies the new rates shortly: reload to see them.";
  });
}
