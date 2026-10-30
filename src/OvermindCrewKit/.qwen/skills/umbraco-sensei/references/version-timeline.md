# Umbraco Version Timeline

Verified against umbraco.com LTS page, endoflife.date and docs.umbraco.com as of Oct 2026.
Policy: two majors per year (roughly June and November), every fourth major is LTS aligned
with a .NET LTS. STS: ~9 months support + ~3 months security. LTS: 24 months support +
12 months security-only.

| Major | Type | .NET | Released | Support ends | EOL | Headline |
|-------|------|------|----------|--------------|-----|----------|
| 8 | legacy | .NET Framework 4.7.2 | Feb 2019 | — | Feb 2025 | last .NET Framework line, AngularJS backoffice |
| 9 | STS | .NET 5 | Sep 2021 | — | dead | first ASP.NET Core Umbraco |
| 10 | LTS | .NET 6 | Jun 2022 | — | Jun 2025 | first ASP.NET Core LTS |
| 11 | STS | .NET 7 | Dec 2022 | — | dead | Block Grid shipped |
| 12 | STS | .NET 7 | Jun 2023 | — | dead | Content Delivery API |
| 13 | **LTS** | .NET 8 | 14 Dec 2023 | 14 Dec 2025 | **14 Dec 2026** | last AngularJS backoffice, Webhooks, Dynamic Root |
| 14 | STS | .NET 8 | 30 May 2024 | — | 30 May 2025 | **Bellissima** backoffice (Lit/Web Components), Management API, System.Text.Json |
| 15 | STS | .NET 9 | 14 Nov 2024 | — | 14 Nov 2025 | **HybridCache** replaces NuCache, lazy-loaded cache, Tiptap RTE added, block-level variants |
| 16 | STS | .NET 9 | 12 Jun 2025 | — | 12 Jun 2026 | TinyMCE removed, Tiptap only; async package migrations |
| 17 | **LTS** | .NET 10 | 27 Nov 2025 | 11 Jun 2026 → security to 27 Nov 2027 | **27 Nov 2028** | UTC system dates, Razor runtime compilation split out, load balancing & semantic search focus |
| 18 | STS | .NET 10 | 25 Jun 2026 | — | 25 Jun 2027 | **Elements** + Library section, typed OpenAPI for Delivery API, Swashbuckle dropped for MS OpenAPI, big obsolete-code purge |
| 19 | STS (planned) | — | ~Nov 2026 | — | — | Elements inside Block Editor (announced, unverified) |

Note on 17 dates: the official LTS page lists support phase ending 11 Jun 2026 and the security
phase running to 27 Nov 2027, with EOL 27 Nov 2028. Third-party blogs disagree on the middle
dates. Trust umbraco.com/products/knowledge-center/long-term-support-and-end-of-life.

## Upgrade Path Rules
- Upgrade one major at a time is NOT required, but 18 only upgrades from **16.4+**; the
  recommended path from 13 is **13 → 17 → 18**.
- 13 → 14 is the hard wall: every backoffice customization (AngularJS, `package.manifest`,
  `UmbracoAuthorizedApiController`) must be rewritten for Bellissima. Budget it as a rewrite of
  the backoffice layer, not an upgrade.
- 14 → 15: ModelsBuilder models must be regenerated (`IPublishedSnapshot` gone).
- 15 → 16: TinyMCE data types auto-migrate to Tiptap; install the community TinyMCE package
  BEFORE upgrading if you need to keep it. Packages compiled against 15 that use migrations
  break at runtime on 16 (binary-incompatible async base class).
- 16 → 17: dates become UTC (migration runs), Razor runtime compilation and InMemoryAuto
  ModelsBuilder need the `Umbraco.Cms.DevelopmentMode.Backoffice` package.
- 17 → 18: empty the media recycle bin first; `UmbracoApiController` gone; services split;
  `Parent`/`Children` properties → extension methods; Swagger → OpenAPI endpoints.

## Which version to recommend
- New project, no constraints → **17 LTS**. Stable until Nov 2028.
- Need Elements / typed Delivery API schemas → 18, accepting the STS treadmill.
- Still on 13 → plan the 17 move before Dec 2026, the backoffice rewrite dominates the estimate.
- Anything ≤12 → it is EOL, security fixes stopped, treat as an incident.
