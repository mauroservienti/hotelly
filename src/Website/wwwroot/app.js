// The Hotelly search page. It knows nothing about services: it sends the
// filters to /search, and renders the facets and the composed hotels it gets
// back. The page's state lives in the address bar, so searches can be shared.

const pageSize = 10;

const sortOptions = [
  ["", "Default"],
  ["stars:desc", "Stars, high to low"],
  ["stars", "Stars, low to high"],
  ["price", "Price, low to high"],
  ["price:desc", "Price, high to low"],
  ["rating:desc", "Rating, high to low"],
  ["rating", "Rating, low to high"],
];

const owners = {
  registry: "Registry",
  reservations: "Reservations",
  sales: "Sales",
  marketing: "Marketing",
  search: "Search",
};

const $ = (id) => document.getElementById(id);

const state = readState();

// Facets only list the values the current results have. The first unfiltered
// search remembers every city and amenity, so they stay selectable.
const known = { city: [], amenities: [] };

let latest = 0;

// Dates

const pad = (n) => String(n).padStart(2, "0");
const iso = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const addDays = (d, days) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + days);
const parseDate = (s) => {
  const [y, m, d] = s.split("-").map(Number);
  return new Date(y, m - 1, d);
};

// The seed data covers 12 months, from the first day of the current month.
const today = new Date();
const seedEnd = new Date(today.getFullYear(), today.getMonth() + 12, 0);

function hasStay() {
  return state.checkIn && state.checkOut && state.checkOut > state.checkIn;
}

function nights() {
  return Math.round((parseDate(state.checkOut) - parseDate(state.checkIn)) / 86400000);
}

// State and the request

function readState() {
  const q = new URLSearchParams(location.search);
  const list = (name) => new Set((q.get(name) || "").split(",").filter(Boolean));
  const [sort = "", thenBy = ""] = (q.get("orderBy") || "").split(",");
  return {
    city: q.get("city") || "",
    stars: list("stars"),
    amenities: list("amenities"),
    rating: q.get("rating") || "",
    checkIn: q.get("checkIn") || "",
    checkOut: q.get("checkOut") || "",
    sort,
    thenBy,
    page: Math.max(1, Number(q.get("page")) || 1),
  };
}

function query() {
  const q = new URLSearchParams();
  if (state.city) q.set("city", state.city);
  if (state.stars.size) q.set("stars", [...state.stars].join(","));
  if (state.amenities.size) q.set("amenities", [...state.amenities].join(","));
  if (state.rating) q.set("rating", state.rating);
  if (hasStay()) {
    q.set("checkIn", state.checkIn);
    q.set("checkOut", state.checkOut);
  }
  const orderBy = [state.sort, state.thenBy].filter(Boolean);
  if (orderBy.length) q.set("orderBy", orderBy.join(","));
  if (state.page > 1) q.set("page", state.page);
  return q;
}

// Changing anything but the page starts from the first page again.
function update(change, keepPage = false) {
  change();
  if (!keepPage) state.page = 1;
  search();
}

async function search() {
  const q = query();
  history.replaceState(null, "", q.size ? `?${q}` : location.pathname);

  q.set("pageSize", pageSize);
  const url = `/search?${q}`;
  $("request-line").textContent = `GET ${decodeURIComponent(url)}`;
  $("how-request").textContent = `GET ${decodeURIComponent(url)}`;
  $("hotels").classList.add("loading");

  const id = ++latest;
  const started = performance.now();
  try {
    const response = await fetch(url);
    if (!response.ok) throw new Error(`The gateway answered ${response.status}.`);
    const result = await response.json();
    if (id !== latest) return; // A newer search is on its way.

    $("timing").textContent = `${Math.round(performance.now() - started)} ms`;
    $("response").textContent = JSON.stringify(result, null, 2);
    $("error").hidden = true;
    render(result);
  } catch (error) {
    if (id !== latest) return;
    $("error").textContent = `The search failed: ${error.message} Is the composition gateway running?`;
    $("error").hidden = false;
  } finally {
    if (id === latest) $("hotels").classList.remove("loading");
  }
}

// Rendering

function facet(result, name) {
  const values = result.facets.find((f) => f.name === name)?.values ?? [];
  return new Map(values.map((v) => [v.value, v.count]));
}

function remember(name, counts) {
  for (const value of counts.keys()) {
    if (!known[name].includes(value)) known[name].push(value);
  }
}

function option(type, name, value, label, count, checked, onChange) {
  const el = document.createElement("label");
  el.className = "option" + (count === 0 ? " empty" : "");
  const input = document.createElement("input");
  input.type = type;
  input.name = name;
  input.value = value;
  input.checked = checked;
  input.addEventListener("change", onChange);
  const text = document.createElement("span");
  text.textContent = label;
  const counter = document.createElement("span");
  counter.className = "count";
  counter.textContent = count ?? "";
  el.append(input, text, counter);
  return el;
}

function render(result) {
  const cities = facet(result, "city");
  const stars = facet(result, "stars");
  const amenities = facet(result, "amenities");
  const ratings = facet(result, "rating");
  remember("city", cities);
  remember("amenities", amenities);

  // City
  const city = $("city");
  city.replaceChildren(new Option("Any city", ""));
  for (const name of [...known.city].sort()) {
    city.append(new Option(`${name} (${cities.get(name) ?? 0})`, name, false, name === state.city));
  }
  city.value = state.city;

  // Stars, from 5 down to 1
  $("stars").replaceChildren(...[5, 4, 3, 2, 1].map((n) =>
    option("checkbox", "stars", n, "★".repeat(n), stars.get(String(n)) ?? 0, state.stars.has(String(n)),
      (e) => update(() => e.target.checked ? state.stars.add(String(n)) : state.stars.delete(String(n))))));

  // Amenities
  $("amenities").replaceChildren(...[...known.amenities].sort().map((a) =>
    option("checkbox", "amenities", a, a.replace("-", " "), amenities.get(a) ?? 0, state.amenities.has(a),
      (e) => update(() => e.target.checked ? state.amenities.add(a) : state.amenities.delete(a)))));

  // Rating bands, plus "any"
  $("rating").replaceChildren(
    option("radio", "rating", "", "Any rating", null, !state.rating, () => update(() => (state.rating = ""))),
    ...["4", "3", "2"].map((band) =>
      option("radio", "rating", band, `${band} and up`, ratings.get(band) ?? 0, state.rating === band,
        () => update(() => (state.rating = band)))));

  // Summary and pages
  const pages = Math.max(1, Math.ceil(result.totalCount / pageSize));
  const stay = hasStay() ? `, available for ${nights()} night${nights() === 1 ? "" : "s"}` : "";
  $("summary").textContent = `${result.totalCount} hotel${result.totalCount === 1 ? "" : "s"}${stay}`;
  $("page-info").textContent = `Page ${state.page} of ${pages}`;
  $("prev").disabled = state.page <= 1;
  $("next").disabled = state.page >= pages;

  $("hotels").replaceChildren(...result.hotels.map(hotel));
}

const euros = new Intl.NumberFormat(undefined, { style: "currency", currency: "EUR", maximumFractionDigits: 0 });

function hotel(h) {
  const el = $("hotel-template").content.firstElementChild.cloneNode(true);
  el.querySelector(".name").textContent = h.name;
  const stars = el.querySelector(".stars");
  stars.textContent = "★".repeat(h.stars);
  stars.setAttribute("aria-label", `${h.stars} stars`);
  el.querySelector(".city").textContent = h.city;
  el.querySelector(".amenities").replaceChildren(...h.amenities.map((a) => {
    const li = document.createElement("li");
    li.textContent = a.replace("-", " ");
    return li;
  }));

  const rating = el.querySelector(".rating");
  if (h.rating == null) {
    rating.textContent = "No reviews yet";
    rating.classList.add("muted");
  } else {
    rating.innerHTML = `<strong>${h.rating.toFixed(1)}</strong> / 5 <span class="muted">· ${h.reviewCount} review${h.reviewCount === 1 ? "" : "s"}</span>`;
  }

  const price = el.querySelector(".price");
  if (h.stayPrice != null) {
    price.innerHTML = `<strong>${euros.format(h.stayPrice)}</strong> <span class="muted">for ${nights()} night${nights() === 1 ? "" : "s"}</span>`;
  } else if (h.fromPrice != null) {
    price.innerHTML = `<span class="muted">from</span> <strong>${euros.format(h.fromPrice)}</strong> <span class="muted">a night</span>`;
  } else {
    price.textContent = "No price in the next 30 days";
    price.classList.add("muted");
  }

  // Hovering a piece of data says who added it.
  for (const part of el.querySelectorAll("[data-owner]")) {
    part.title = `Added by ${owners[part.dataset.owner]}'s composition handler, from ${owners[part.dataset.owner]}'s database`;
  }
  return el;
}

// Wiring

function ownerBadges() {
  for (const group of document.querySelectorAll(".group[data-owner], .sorts[data-owner]")) {
    const badges = document.createElement("span");
    badges.className = "owners";
    for (const owner of group.dataset.owner.split(" ")) {
      const badge = document.createElement("span");
      badge.className = "owner";
      badge.dataset.ownerName = owner;
      badge.style.setProperty("--owner", `var(--${owner})`);
      badge.textContent = owners[owner];
      badges.append(badge);
    }
    const target = group.querySelector("legend") ?? group;
    target.append(badges);
  }
}

function fillSorts() {
  for (const [select, value] of [[$("sort"), state.sort], [$("then-by"), state.thenBy]]) {
    select.replaceChildren(...sortOptions.map(([v, label]) => new Option(label, v, false, v === value)));
    select.value = value;
  }
  $("then-by").disabled = !state.sort;
}

function wire() {
  const checkIn = $("check-in");
  const checkOut = $("check-out");
  checkIn.min = iso(today);
  checkIn.max = iso(addDays(seedEnd, -1));
  checkOut.min = iso(addDays(today, 1));
  checkOut.max = iso(addDays(seedEnd, 1));
  checkIn.value = state.checkIn;
  checkOut.value = state.checkOut;
  $("seed-window").textContent = `The demo has prices and availability until ${seedEnd.toLocaleDateString(undefined, { dateStyle: "long" })}.`;

  checkIn.addEventListener("change", () => update(() => {
    state.checkIn = checkIn.value;
    // Picking a check-in suggests a two-night stay.
    if (state.checkIn && (!state.checkOut || state.checkOut <= state.checkIn)) {
      state.checkOut = checkOut.value = iso(addDays(parseDate(state.checkIn), 2));
    }
    checkOut.min = state.checkIn ? iso(addDays(parseDate(state.checkIn), 1)) : iso(addDays(today, 1));
  }));
  checkOut.addEventListener("change", () => update(() => (state.checkOut = checkOut.value)));
  $("clear-dates").addEventListener("click", () => update(() => {
    state.checkIn = state.checkOut = checkIn.value = checkOut.value = "";
  }));

  $("city").addEventListener("change", (e) => update(() => (state.city = e.target.value)));

  $("sort").addEventListener("change", (e) => update(() => {
    state.sort = e.target.value;
    if (!state.sort || state.thenBy.split(":")[0] === state.sort.split(":")[0]) state.thenBy = "";
    fillSorts();
  }));
  $("then-by").addEventListener("change", (e) => update(() => {
    state.thenBy = e.target.value;
    // Sorting twice by the same key does nothing.
    if (state.thenBy.split(":")[0] === state.sort.split(":")[0]) state.thenBy = "";
    fillSorts();
  }));

  $("prev").addEventListener("click", () => update(() => state.page--, true));
  $("next").addEventListener("click", () => update(() => state.page++, true));

  $("show-owners").addEventListener("change", (e) => document.body.classList.toggle("show-owners", e.target.checked));

  $("how-toggle").addEventListener("click", () => {
    const how = $("how");
    how.hidden = !how.hidden;
    $("how-toggle").setAttribute("aria-expanded", String(!how.hidden));
  });
}

async function start() {
  ownerBadges();
  fillSorts();
  wire();

  // An unfiltered search first, to learn every city and amenity, unless the
  // page opened unfiltered anyway.
  if (location.search) {
    try {
      const all = await (await fetch("/search?pageSize=1")).json();
      remember("city", facet(all, "city"));
      remember("amenities", facet(all, "amenities"));
    } catch {
      // The real search reports the error.
    }
  }
  search();
}

start();
