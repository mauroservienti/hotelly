// Marketing's back office panel: the hotel's rating and latest reviews. Guests
// write reviews, not hotel managers, so the panel is read-only. Part of
// Marketing, deployed with the website.

export const title = "Marketing: ratings and reviews";

const styles = document.createElement("link");
styles.rel = "stylesheet";
styles.href = new URL("panel.css", import.meta.url);
document.head.append(styles);

const date = new Intl.DateTimeFormat(undefined, { day: "numeric", month: "short", year: "numeric" });

const text = (value) => {
  const span = document.createElement("span");
  span.textContent = value;
  return span.innerHTML;
};

// Nothing to collect: a new hotel has no reviews yet.
export function create(element) {
  element.innerHTML = `<p class="muted">New hotels start with no reviews. Guests review them after their stay.</p>`;
  return () => undefined;
}

export function edit(element, hotel) {
  element.innerHTML = hotel.rating == null
    ? `<p class="muted">No reviews yet.</p>`
    : `<p class="marketing-rating"><strong>${hotel.rating.toFixed(1)}</strong> / 5 <span class="muted">· ${hotel.reviewCount} review${hotel.reviewCount === 1 ? "" : "s"}</span></p>
       <ul class="marketing-reviews">${hotel.latestReviews.map((r) => `
         <li><span class="meta">${"★".repeat(r.rating)} · ${date.format(new Date(`${r.reviewedOn}T00:00`))}</span><br>${text(r.comment)}</li>`).join("")}
       </ul>`;
}
