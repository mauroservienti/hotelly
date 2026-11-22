// Reservations' back office panel: the hotel's rooms and bookings. Part of
// Reservations, deployed with the website.

export const title = "Reservations: rooms";

const styles = document.createElement("link");
styles.rel = "stylesheet";
styles.href = new URL("panel.css", import.meta.url);
document.head.append(styles);

const pad = (n) => String(n).padStart(2, "0");
const iso = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const addDays = (d, days) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + days);
const day = new Intl.DateTimeFormat(undefined, { weekday: "short", day: "numeric", month: "short" });

export function create(element) {
  element.innerHTML = `
    <div class="fields">
      <label>Rooms for the next year <input type="number" name="rooms" min="1" step="1" required></label>
    </div>`;
  return () => ({ rooms: Number(element.querySelector('[name="rooms"]').value) });
}

export function edit(element, hotel, save) {
  const nights = hotel.availability ?? [];
  const list = document.createElement("ul");
  list.className = "reservations-nights";
  list.innerHTML = nights.length === 0
    ? `<li>No rooms in the next two weeks</li>`
    : nights.map((n) => `<li class="${n.booked >= n.rooms ? "sold-out" : ""}">${day.format(new Date(`${n.night}T00:00`))}<strong>${n.booked} / ${n.rooms}</strong>booked</li>`).join("");

  const today = new Date();
  const form = document.createElement("form");
  form.innerHTML = `
    <div class="reservations-range">
      <label>From <input type="date" name="from" required value="${iso(today)}" min="${iso(today)}"></label>
      <label>Until, excluded <input type="date" name="until" required value="${iso(addDays(today, 7))}"></label>
      <label>Rooms <input type="number" name="count" min="1" step="1" required></label>
    </div>
    <p class="saved">A night can't have fewer rooms than bookings: it keeps one room per booking.</p>
    <div class="actions"><span class="saved" role="status"></span> <button type="submit">Set rooms</button></div>`;
  element.append(list, form);

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    const field = (name) => form.querySelector(`[name="${name}"]`).value;
    const saved = form.querySelector('.saved[role="status"]');
    saved.textContent = "Saving…";
    const error = await save({ from: field("from"), until: field("until"), count: Number(field("count")) });
    saved.dataset.kind = error ? "error" : "info";
    saved.textContent = error ?? "Saved. Reservations applies the change shortly: reload to see it.";
  });
}
