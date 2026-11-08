# Hotelly

A showcase demoing hotel booking and management, designed applying service-oriented architecture (SOA) principles.

Hotelly is made of the following services:

- **Registry:** hotels, their location, stars, and amenities.
- **Reservations:** rooms availability and bookings.
- **Marketing:** ratings and reviews.
- **Sales:** nightly rates.

## Searching, paging, and sorting

The first scenario Hotelly demos is a hotel search page: filtering, faceting, sorting, and paging data owned by different services. Each service owns its PostgreSQL server. Search has its own server too, with one schema per service. Each service has a database role that can write only to its own schema, and the search process has a role that can only read.

Services keep their search tables up to date with PostgreSQL logical replication: each service publishes only the columns search needs, and subscribes its schema in the search database to that publication. There's no sync code, and changes arrive in commit order. Publications can also filter rows: Reservations publishes only sold-out nights, the only thing search needs to know about availability. When a booking sells out a night, the row arrives in search as an insert; when a cancellation frees a room, as a delete. The search results page is then composed using [ServiceComposer](https://github.com/ServiceComposer/ServiceComposer.AspNetCore).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/), or another container runtime supported by [Aspire](https://aspire.dev/)

## How to run

Hotelly uses Aspire to start everything it needs:

```shell
dotnet run --project src/Hotelly.AppHost
```

The Aspire dashboard opens in the browser. From there, pgAdmin gives access to the databases. The PostgreSQL containers have no data volume: every run starts from scratch, and each service seeds its own data at startup.

## Try it

The composition gateway exposes the search at `/search`. For example, hotels in Rome with 4 or 5 stars and a pool, available from February 12 to 15, 2027, sorted by stars, then by rating:

```shell
curl "http://localhost:5100/search?city=Rome&stars=4,5&amenities=pool&checkIn=2027-02-12&checkOut=2027-02-15&orderBy=stars:desc,rating:desc"
```

The response contains a page of hotel IDs, the total number of matching hotels, and the facets. `checkIn`, `checkOut`, `orderBy`, `page`, and `pageSize` are reserved; every other parameter is a filter, handled by the service that owns it.

## Search providers

Services take part in the search by deploying providers in the composition gateway, the same way they deploy composition handlers. The search process starts from the hotels, provided by Registry, and knows nothing about the services' data: providers build the queries, and everything runs in the search database.

- **Filters:** every filter provider runs on every request. Each one reads the filters it owns, like `city` or `amenities`, and does nothing when they're missing.
- **Facets:** every facet provider runs on every request, over the filtered hotels, before paging.
- **Sorts:** only the requested ones run, in the requested order. `orderBy=stars:desc,price` sorts by stars first, then by price. That's why sort providers expose a `Key`: the search process picks the provider for each requested key, and tells it whether it's the first sort (`OrderBy`) or a following one (`ThenBy`). Without keys, providers owned by different services would have to agree among themselves on who goes first. Keys also make conflicts visible: if two services claim the same key, the gateway fails at startup.

Filter names and sort keys are the contract search imposes on services and on the UI. Hotelly is a demo, so filters don't declare their names: unknown filters are silently ignored, and nothing detects two services handling the same filter. Giving filter providers a key, like sort providers, would fix both.
