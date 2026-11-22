// The Hotelly back office. The page is a shell: it knows nothing about
// hotels. Services ship their panels as JavaScript modules, deployed with
// the website, and the page asks the website which ones to load.
//
// A panel module exports:
// - title: the panel's heading.
// - create(element): renders the fields for a new hotel, and returns a
//   function reading them as the service's part of the request.
// - edit(element, hotel, save): renders the service's data from the composed
//   hotel, and calls save(body) to save it on its own.
//
// Creating a hotel is one request, POST /back-office/hotels/{id}, with one
// part for each service: each service's composition handler in the gateway
// reads its part and sends a command to its back end, all in one
// transactional session. The hotel ID is generated here, so retrying the
// request can't create the hotel twice. Editing is one request per panel.

const $ = (id) => document.getElementById(id);
const hotelId = new URLSearchParams(location.search).get("hotel");

$("show-owners").addEventListener("change", (e) =>
  document.body.classList.toggle("show-owners", e.target.checked));

function status(message, kind = "info") {
  const el = $("status");
  el.textContent = message;
  el.dataset.kind = kind;
  el.hidden = !message;
}

async function problem(response) {
  try {
    const body = await response.json();
    return body.detail ?? body.title ?? `${response.status} ${response.statusText}`;
  } catch {
    return `${response.status} ${response.statusText}`;
  }
}

// Writes carry an idempotency key: the gateway uses it as the ID of the
// messages it sends, so a retried request doesn't send them twice.
async function post(path, body, idempotencyKey) {
  return fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json", "Idempotency-Key": `"${idempotencyKey}"` },
    body: JSON.stringify(body),
  });
}

function panelElement(panel, container) {
  const section = document.createElement("section");
  section.className = "panel";
  section.dataset.owner = panel.service;
  const heading = document.createElement("h2");
  heading.textContent = panel.title;
  const body = document.createElement("div");
  section.append(heading, body);
  container.append(section);
  return body;
}

async function loadPanels() {
  const response = await fetch("/back-office/panels");
  const panels = await response.json();
  return Promise.all(panels.map(async (p) => ({ ...p, ...(await import(`/${p.module}`)) })));
}

async function create(panels) {
  const id = crypto.randomUUID();
  $("subtitle").textContent = "Add a hotel. Each service's panel collects the data that service owns, and all of it is sent in one request.";
  const readers = panels.map((panel) => [panel.service, panel.create(panelElement(panel, $("create-panels")))]);
  $("create").hidden = false;

  $("create").addEventListener("submit", async (e) => {
    e.preventDefault();
    const body = Object.fromEntries(readers.map(([service, read]) => [service, read()]));
    status("Adding the hotel…");
    const response = await post(`/back-office/hotels/${id}`, body, id);
    if (!response.ok) {
      status(`The hotel wasn't added: ${await problem(response)}`, "error");
      return;
    }

    // The gateway only sent commands: the services' back ends add the hotel
    // asynchronously, so it can take a moment to show up.
    status("Hotel added. Opening it…");
    setTimeout(() => location.assign(`back-office.html?hotel=${id}`), 1000);
  });
}

async function edit(panels) {
  const response = await fetch(`/back-office/hotels/${hotelId}`);
  if (response.status === 404) {
    status("This hotel isn't there yet. If you just added it, the services may still be processing it: refresh in a moment.", "error");
    return;
  }
  if (!response.ok) {
    status(`The hotel couldn't be loaded: ${await problem(response)}`, "error");
    return;
  }

  const hotel = await response.json();
  $("subtitle").textContent = "Each panel shows the data its service owns, and saves it on its own.";
  for (const panel of panels) {
    const save = async (body) => {
      const saved = await post(`/back-office/hotels/${hotelId}/${panel.service}`, body, crypto.randomUUID());
      return saved.ok ? null : await problem(saved);
    };
    panel.edit(panelElement(panel, $("edit-panels")), hotel, save);
  }
  $("edit-panels").hidden = false;
}

const panels = await loadPanels();
if (panels.length === 0) {
  $("no-panels").hidden = false;
} else if (hotelId) {
  await edit(panels);
} else {
  await create(panels);
}
