// Registry's back office panel: the hotel details Registry owns. Part of
// Registry, deployed with the website.

export const title = "Registry: hotel details";

// The panel brings its own styles, next to the module.
const styles = document.createElement("link");
styles.rel = "stylesheet";
styles.href = new URL("panel.css", import.meta.url);
document.head.append(styles);

const amenities = ["wifi", "parking", "pool", "spa", "gym", "restaurant", "pets", "airport-shuttle"];
const cities = ["Rome", "Milan", "Venice", "Florence", "Naples", "Bologna", "Turin"];

function fields(element, hotel = {}) {
  element.innerHTML = `
    <div class="fields">
      <label>Name <input type="text" name="name" required></label>
      <label>City <input type="text" name="city" list="registry-cities" required></label>
      <datalist id="registry-cities">${cities.map((c) => `<option value="${c}">`).join("")}</datalist>
      <label>Stars
        <select name="stars">${[1, 2, 3, 4, 5].map((s) => `<option value="${s}">${"★".repeat(s)}</option>`).join("")}</select>
      </label>
      <fieldset class="registry-amenities">
        <legend>Amenities</legend>
        ${amenities.map((a) => `<label><input type="checkbox" name="amenities" value="${a}"> ${a.replace("-", " ")}</label>`).join("")}
      </fieldset>
      <label>Description <textarea name="description" rows="3"></textarea></label>
    </div>`;

  const field = (name) => element.querySelector(`[name="${name}"]`);
  field("name").value = hotel.name ?? "";
  field("city").value = hotel.city ?? "";
  field("stars").value = String(hotel.stars ?? 3);
  for (const box of element.querySelectorAll('[name="amenities"]')) {
    box.checked = (hotel.amenities ?? ["wifi"]).includes(box.value);
  }
  field("description").value = hotel.description ?? "";

  return () => ({
    name: field("name").value,
    city: field("city").value,
    stars: Number(field("stars").value),
    amenities: [...element.querySelectorAll('[name="amenities"]:checked')].map((box) => box.value),
    description: field("description").value,
  });
}

export function create(element) {
  return fields(element);
}

export function edit(element, hotel, save) {
  const form = document.createElement("form");
  element.append(form);
  const read = fields(form, hotel);
  const footer = document.createElement("div");
  footer.className = "actions";
  footer.innerHTML = `<span class="saved" role="status"></span> <button type="submit">Save details</button>`;
  form.append(footer);

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    const saved = footer.querySelector(".saved");
    saved.textContent = "Saving…";
    const error = await save(read());
    saved.dataset.kind = error ? "error" : "info";
    saved.textContent = error ?? "Saved. Registry applies the change shortly.";
  });
}
