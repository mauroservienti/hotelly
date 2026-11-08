# Hotelly

A showcase demoing hotel booking and management, designed applying service-oriented architecture (SOA) principles.

Hotelly is made of the following services:

- **Registry:** hotels, their location, stars, and amenities.
- **Reservations:** rooms availability and bookings.
- **Marketing:** ratings and reviews.
- **Sales:** nightly rates.

## Searching, paging, and sorting

The first scenario Hotelly demos is a hotel search page: filtering, faceting, sorting, and paging data owned by different services. Each service owns its PostgreSQL server. Search has its own server too, with one schema per service. Each service has a database role that can write only to its own schema, and the search process has a role that can only read.

Services keep their search tables up to date with PostgreSQL logical replication: each service publishes only the columns search needs, and subscribes its schema in the search database to that publication. There's no sync code, and changes arrive in commit order. The search results page is then composed using [ServiceComposer](https://github.com/ServiceComposer/ServiceComposer.AspNetCore).

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

The composition gateway exposes the search at `/search`. For example, hotels in Rome with 4 or 5 stars and a pool, sorted by stars:

```shell
curl "http://localhost:5100/search?city=Rome&stars=4,5&amenities=pool&orderBy=stars:desc"
```

The response contains a page of hotel IDs, the total number of matching hotels, and the facets. `checkIn`, `checkOut`, `orderBy`, `page`, and `pageSize` are reserved; every other parameter is a filter, handled by the service that owns it.
