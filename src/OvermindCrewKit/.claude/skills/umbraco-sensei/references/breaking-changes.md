# Umbraco Breaking Changes by Major (10 → 18)

Source: docs.umbraco.com version-specific upgrade notes (13.latest page for 10-13, 18.latest for
14-18), releases.umbraco.com, umbraco.com release blogs. Verified as of Umbraco 18.0 (Oct 2026).
For 19+ and patch-level details: check
`docs.umbraco.com/umbraco-cms/<major>.latest/get-started/upgrading-and-migrating/version-specific`
(for ≤13 the path is `.../fundamentals/setup/upgrading/version-specific`).

Legend: **[rewrite]** = code must be rewritten, **[recompile]** = rebuild against the new major,
**[config]** = settings change, **[data]** = migration touches stored data, **[behavior]** =
runtime behavior changes silently.

Skipping majors inside 10 → 13 is supported at the data level, but the compile errors from every
skipped major land on you at once. Read all intermediate sections.

---

## Umbraco 10 (.NET 6, LTS) — SQLite in, SQL CE out

Requires .NET 6.0.5+. The first LTS on ASP.NET Core; 9 was the pioneer, 10 is where the API settled.

| Change | Kind | What to do |
|--------|------|-----------|
| SQL CE removed, **SQLite** added; lock methods removed from `ISqlSyntaxProvider`, SQL Server code moved, new syntax-provider members | [rewrite] | Dev DBs move to SQLite. Custom syntax providers must implement the new members. |
| NPoco 4 → 5 (NPoco types are public API) | [recompile]/[rewrite] | Anything touching NPoco directly. |
| Examine 3: `ValueSet` immutable, `ValueSetValidationResult` renamed | [rewrite] | Custom indexers / value set builders. |
| ImageSharp 2 (`ImageSharp.Web` 2.0): `CachedNameLength` → `CacheHashLength`, config split | [config] | Fix `appsettings` imaging section. |
| `ContentSchedule` mutable property removed | [rewrite] | Use `GetContentScheduleByContentId` / `PersistContentSchedule` or the `Save` parameter. |
| `PublishedSnapshotServiceEventHandler` public surface removed | [rewrite] | — |
| Scope provider classes moved to `Umbraco.Cms.Core` out of `Services.Implement` | [recompile] | Fix usings. |
| Member lock-out / approval / login dates are real columns; nullable dates return `null` not `DateTime.MinValue` | [behavior] | Audit member code comparing to `MinValue`. |
| `TryFindContent` (content finders) and tree `Search` → async (`SearchAsync`) | [rewrite] | Custom content finders and tree controllers. |
| New members on redirect interfaces; `IPublishedModelFactory.GetModelType` added | [rewrite] | Custom implementations. |
| `StackQueue` → `Umbraco.Cms.Core.Collections` | [recompile] | — |
| `SqlWriteLockTimeOut` removed → `DistributedLockingWriteLockDefaultTimeout`; `UmbracoPath` hardcoded to `/umbraco` | [config] | — |
| Backoffice diff lib 3.5 → 5.0, global `JsDiff` → `Diff` | [rewrite] | AngularJS plugins using it. |

## Umbraco 11 (.NET 7, STS) — the obsolete purge

| Change | Kind | What to do |
|--------|------|-----------|
| .NET 7 / ASP.NET Core 7 | [recompile] | Microsoft's own breaking list applies. |
| Nullable reference types enabled across the codebase, signatures changed | [recompile] | Expect a wall of nullability warnings/errors. |
| Everything marked obsolete since 9 removed (static helpers, member conventions, service constructors) | [rewrite] | Fix the obsolete warnings on 10 BEFORE moving. |
| TinyMCE upgraded: plugins and custom config must be migrated | [rewrite] | AngularJS-era RTE plugins. |
| ImageSharp and fault-handling types moved to new assemblies/namespaces | [recompile] | Fix usings. |
| Merged interfaces (macro, two-factor, type-related) gained members | [rewrite] | Custom implementations. |
| `ContentData` immutable | [rewrite] | NuCache-level customizations. |
| Several embed providers inherit `OEmbedProviderBase` instead of `EmbedProviderBase` | [rewrite] | Custom embed providers. |
| **Block Grid** editor shipped (new, not breaking) | — | The modern replacement for Grid Layout starts here. |

## Umbraco 12 (.NET 7, STS) — Delivery API arrives

| Change | Kind | What to do |
|--------|------|-----------|
| Migrations: each migration runs in its own transaction (for SQLite) | [behavior] | Multi-step custom migrations lose atomicity across steps. |
| Migration APIs: `Execute` → `ExecutePlan`; `AddPostMigration` removed in favor of notifications; `Upgrader.Execute` obsolete, now takes `ICoreScopeProvider` | [rewrite] | Package migration plans. |
| `PagedModel` namespace changed (`Umbraco.New.Cms.Core.Models` → `Umbraco.Cms.Core.Models`); several post-migrations and types removed | [recompile] | — |
| Delivery API: `PublishedPropertyBase` must implement `GetDeliveryApiValue` | [rewrite] | Custom published property implementations. |
| New interface members without default impls on `IEventAggregator`, `IPublishedProperty`, `IPublishedPropertyType`, `ICoreScope`, `IMigrationPlanExecutor`, indexing interfaces | [rewrite] | Any custom implementation of those. |
| **Content Delivery API** shipped, **EF Core** support added (new, not breaking) | — | Headless starts here. |

## Umbraco 13 (.NET 8, LTS) — last AngularJS backoffice

| Change | Kind | What to do |
|--------|------|-----------|
| Fallback languages: ISO codes replace language IDs | [rewrite] | Custom fallback logic. |
| Delivery API breaking changes (#14745): response shapes and querying | [rewrite] | Headless consumers built on 12. |
| New backoffice login screen | [behavior] | Login customizations. |
| `JsonNetSerializer` settings no longer leak into derived implementations | [behavior] | Custom serializers. |
| Default property value converters for all value types | [behavior] | Value types that previously came back raw now convert. |
| Nested property values not indexed explicitly by default | [behavior] | Examine queries over Block content may stop matching; add explicit indexing. |
| Rich Text Editor gains Blocks | — | New, not breaking. |
| Dynamic root query step fixes (`FurthestAncestorOrSelf…`, `FurthestDescendantOrSelf…`) | [behavior] | Pickers using dynamic roots. |
| Nullability removed from `IImageSourceParser`, `ILocalLinkParser`, `IMacroParser` | [recompile] | — |
| `PackageMigrationsPlans` collection is weighted, not lazy | [behavior] | Order of package migrations. |
| **Webhooks** shipped; `IWebHookService` → `IWebhookService`, webhooks are `IEntity`, `WebhookEventCollectionBuilder` sets the collection, date header removed | [rewrite] | Only if you were on the 13 RC API. |
| `ClearCsrfCookies` post-migration removed → `UmbracoPlanExecutedNotification` handler | [rewrite] | — |
| Config to limit concurrent logins | [config] | New option. |
| Deploy: `IContextCache` moved to base interfaces, checksum on artifact dependency | [rewrite] | Deploy connectors. |
| EF Core Design 8.0.0 clashes with `Microsoft.CodeAnalysis.Common` | [config] | Pin `Microsoft.CodeAnalysis.CSharp.Workspaces` to 4.8.0. |

---

## The AngularJS world (10 → 13), for legacy work

Everything below is dead from 14 on. Keep it in your head for 13 maintenance and for estimating
the 13 → 17 rewrite.

- Extensions: `App_Plugins/<Name>/package.manifest` (JSON) listing JS/CSS + `propertyEditors`,
  `dashboards`, `sections`, `contentApps`. Views are AngularJS HTML + controllers registered on
  `angular.module("umbraco")`. Resources via `$http` to `UmbracoAuthorizedJsonController`.
- Property editors: `package.manifest` `propertyEditors[]` with `editor.view`, `prevalues`;
  server side `[DataEditor("alias")]` + `IDataValueEditor` + `ConfigurationEditor`.
- Content Apps: `IContentAppFactory`, become **Workspace Views** in 14.
- Dashboards: `IDashboard` + manifest `dashboards[]`.
- Backoffice controllers: `UmbracoAuthorizedApiController` / `UmbracoAuthorizedJsonController`
  routed under `/umbraco/backoffice/api/...`; front-end: `UmbracoApiController` under `/umbraco/api/...`.
- Macros still exist (removed in 14). Nested Content, Grid Layout, legacy Media Picker still
  exist (removed in 14; migrate them while on 13).
- Serialization: Newtonsoft.Json everywhere (STJ in 14).
- Cache: NuCache (`IPublishedSnapshotAccessor`) until 15.
- RTE: TinyMCE, configured via data type prevalues (Tiptap from 15, TinyMCE gone in 16).

---

## Umbraco 14 (.NET 8) — the Bellissima wall

| Change | Kind | What to do |
|--------|------|-----------|
| AngularJS backoffice replaced by Bellissima (Lit + Web Components, extension-first) | [rewrite] | Every custom section, dashboard, property editor UI, content app → rewrite as Bellissima extension (`umbraco-package.json` manifest, TS + Lit, Vite build). Content Apps are now Workspace Views. |
| `package.manifest` → `umbraco-package.json` | [rewrite] | New manifest format with typed extension kinds. |
| Backoffice `/umbraco/api` controllers → Management API | [rewrite] | `UmbracoAuthorizedApiController` and `UmbracoAuthorizedJsonController` removed. Write Management API controllers (`ManagementApiControllerBase`) with OpenAPI. |
| Newtonsoft.Json → System.Text.Json | [recompile]/[behavior] | STJ is stricter: case, polymorphism, nulls. Anything relying on Newtonsoft leniency breaks. |
| Nested Content, Grid Layout, legacy Media Picker removed | [data] | Migrate to Block List / Block Grid / new Media Picker BEFORE upgrading (do it on 13). |
| Macros and Partial View Macros removed | [rewrite] | Replace with partial views, view components, or Blocks in the RTE. |
| XPath removed | [rewrite] | Use `IPublishedContentQuery`, navigation services, or Examine. |
| Smidge dropped from default install | [config] | Bundle front-end assets with Vite or your own pipeline. |
| UI translations: XML → JS modules | [rewrite] | Custom translations become a `localization` extension. |
| Icons converged on Lucide | [behavior] | Custom icon aliases may not resolve. |
| TinyMCE config moved to `tinyMcePlugin` extensions | [rewrite] | Then removed entirely in 16, see below. |
| Property editors split into server part and client UI (`EditorUiAlias`) | [rewrite] | Value converters should match on `EditorUiAlias` for manifest-based editors. |
| Notifications can no longer mutate models sent through the API | [behavior] | Move logic to server-side notification handlers or Management API extensions. |
| `UmbracoApiController` output now camelCase | [behavior] | Front-end consumers of custom API controllers may break on casing. |
| Webhook payload property names camelCase | [behavior] | Fix consumers. |
| 2FA and external login providers need client registrations (`mfaLoginProvider`, `authProvider`) | [rewrite] | Register a Bellissima extension per provider. |
| Obsolete AppSettings no-op: `ShowDeprecatedPropertyEditors`, `HideBackOfficeLogo`, `IconsPath`, `AllowedMediaHosts` | [config] | Remove. |
| `Microsoft.Data.SQlite` provider name → `Microsoft.Data.Sqlite` | [config] | Fix the connection string provider name. |

## Umbraco 15 (.NET 9) — HybridCache

| Change | Kind | What to do |
|--------|------|-----------|
| NuCache → HybridCache (`Microsoft.Extensions.Caching.Hybrid`), lazy-loaded content cache | [behavior] | Startup loads primary content, rest on demand. Memory drops, cold-path reads may hit DB. Perf-test large sites, especially unusual traversal patterns. Can be backed by a distributed cache. |
| `IPublishedSnapshot` / `IPublishedSnapshotAccessor` removed | [rewrite]/[recompile] | Inject `IPublishedContentCache` / `IPublishedMediaCache` directly. **Regenerate ModelsBuilder models** — generated code referenced the snapshot. |
| Tiptap RTE added (default for new data types), TinyMCE still shipped | [behavior] | Two RTEs coexist in 15 only. |
| Block-level variants | [behavior] | Blocks inside variant properties can now vary per culture; check value converters. |
| Static JS/CSS precompressed (`.br` / `.gz`) | [config] | Set `CompressionEnabled: false` in the CMS project if your hosting layer compresses. |

## Umbraco 16 (.NET 9) — TinyMCE gone

| Change | Kind | What to do |
|--------|------|-----------|
| TinyMCE removed server and client side; only Tiptap ships (TinyMCE license change vs MIT) | [data]/[rewrite] | Existing TinyMCE data types auto-migrate to Tiptap: markup and blocks survive, **toolbar config must be redone**, custom / premium TinyMCE plugins are dead. To keep TinyMCE: install the community `Umbraco.Community.TinyMCE`-style package BEFORE upgrading, which disables the migration. |
| Package migrations async: new base class, source-compatible, **binary-incompatible** | [recompile] | Packages built against 15 throw `MissingMethodException` at runtime on 16. Recompile every package that ships migrations. |
| Examine registered via a Composer | [behavior] | Custom Examine setup must `[ComposeAfter(typeof(ExamineComposer))]` or it runs too early. |
| Client: consumed contexts may resolve `undefined`; `tryExecute` signature changed; `tryExecuteAndNotify` obsolete | [rewrite] | Null-check contexts, update calls. |
| `urls` no longer populated on document/media detail responses | [behavior] | Fetch URLs via the dedicated endpoint. |
| Dependency bumps | [recompile] | Low impact. |

## Umbraco 17 (.NET 10, LTS) — UTC and dev-mode split

| Change | Kind | What to do |
|--------|------|-----------|
| System dates stored in **UTC** (previously server-local without tz) | [data]/[behavior] | Upgrade migration converts existing values using the server time zone; configurable or disableable. Anything comparing `CreateDate`/`UpdateDate` to local now must convert. |
| Date picker returns `DateTime` with `Kind = Unspecified` | [behavior] | Treat as wall-clock, convert explicitly. |
| Razor runtime compilation + InMemoryAuto ModelsBuilder moved to `Umbraco.Cms.DevelopmentMode.Backoffice` | [config] | Add the package in dev, keep it out of prod. Microsoft deprecated runtime compilation in .NET 10. |
| Color picker always exposes a `PickedColor` object | [behavior] | Adjust value converters / views. |
| Segmented properties fall back to the default segment when empty | [behavior] | — |
| `IUrlProvider`: new `GetPreviewUrlAsync()`, required `Alias`; `UrlInfo` revamped | [rewrite] | Update custom URL providers. |
| `UseHttps` defaults to `true` | [config] | Local HTTP dev setups need `Umbraco:CMS:Global:UseHttps=false`. |
| Management API fetches must send `credentials: 'include'` | [rewrite] | Custom backoffice fetch calls. |
| Tiptap import path → `@umbraco-cms/backoffice/tiptap`; user components → `@umbraco-cms/backoffice/current-user` | [rewrite] | Fix imports in extensions. |
| `UmbSectionContext.setManifest()` replaced by manifest; section base class changed | [rewrite] | Custom sections. |
| NPoco 5.7 → 6.1, Swashbuckle → 10.x, OpenIddict and Serilog major bumps | [recompile] | Direct NPoco users and Swagger document customizers may need code changes. |
| Unused obsolete extension methods removed | [recompile] | — |

## Umbraco 18 (.NET 10, STS) — Elements and the purge

Preconditions: upgrade from **16.4+ (recommended 17)**; **empty the media recycle bin** first;
custom property editors that nest Block List values must implement `ITypedSingleBlockListProcessor`.

| Change | Kind | What to do |
|--------|------|-----------|
| Swashbuckle dropped; `Microsoft.AspNetCore.OpenApi` instead. Endpoints `/umbraco/swagger/{doc}/swagger.json` → `/umbraco/openapi/{doc}.json`. Spec is OpenAPI 3.1. | [rewrite]/[config] | Want Swagger UI? Install `Swashbuckle.AspNetCore` yourself, use `AddBackOfficeOpenApiDocument()`. Fix client-generation scripts. Class libraries registering OpenAPI docs need `InterceptorsNamespaces` in `.csproj`. Schema ID / operation ID / sub-types / enum filter handler types removed. |
| `UmbracoApiController` and convention-based front-end API routing removed | [rewrite] | Plain ASP.NET Core controllers / Minimal APIs with explicit routes. |
| `IPublishedContent.Parent` and `.Children` properties removed | [rewrite] | Use `Parent()` / `Children()` extension methods. `GetAtRoot()` → `ContentAtRoot()` or navigation query services. |
| `ILocalizationService` → `ILanguageService` + `IDictionaryItemService`; `IFileService` → template / partial view / stylesheet / script services | [rewrite] | Mechanical. |
| Obsolete sync service methods removed, async only | [rewrite] | `await` everything. |
| `MigrationBase` and `PackageMigrationBase` removed → async base classes only | [rewrite] | Finishes what 16 started. |
| `ContentFinderByUrlNew` → `ContentFinderByUrl`, `NewDefaultUrlProvider` → `DefaultUrlProvider` | [rewrite] | Rename. |
| `EfCore*` type names → `EFCore*` (e.g. `EFCoreScope`) | [rewrite] | Rename. |
| Single-mode Block List data types auto-migrate to the Single Block editor | [data] | Implement `ITypedSingleBlockListProcessor` for custom editors nesting block list values, or nested data is lost. |
| `ConfigureSecurityStampOptions` no longer registered | [behavior] | Register yourself if you depended on it. |
| `EnableMediaRecycleBinProtection` defaults to true (recycled files renamed with `.deleted`) | [behavior] | Blob-storage tooling must cope. |
| Markdown converter default → `MarkdigMarkdownToHtmlConverter` (Hey Red deprecated) | [behavior] | Output HTML can differ. |
| "Master Template" → "Layout Template" (obsolete shims until v20) | [behavior] | Rename at leisure. |
| `IHostingEnvironment.ApplicationMainUrl` nullable; `UmbracoHelper.GetDictionaryValue` returns non-nullable `string` | [recompile] | Nullability warnings. |
| `HideBackOfficeLogo` setting removed | [config] | — |
| Dependency bumps: Markdig 1.x, Roslyn 5.0, Serilog hosting 10.x, Asp.Versioning.Mvc 10.x | [recompile] | — |

New (not breaking): **Elements** content type in the new **Library** section (non-routable
reusable content, Element Picker), typed OpenAPI schemas per Document/Element/Media Type for the
Delivery API (opt-in). Elements-in-Block-Editor is announced for 19.

---

## Quick "13 → 17" effort map

| Area | Effort |
|------|--------|
| Backoffice customizations (AngularJS, content apps, dashboards, custom property editor UIs) | rewrite, dominates the estimate |
| Custom backoffice API controllers | rewrite to Management API |
| Front-end Razor / view components / Composers / notification handlers | mostly recompile, fix nullability, drop snapshot usage |
| Data types: Nested Content, Grid Layout, legacy Media Picker, TinyMCE | migrate on 13 first (NC/Grid/MediaPicker), TinyMCE → Tiptap toolbar redo |
| Packages | each must have a ≥17 build; audit before starting |
| Dates | audit any code comparing system dates with local time |
| Hosting | `UseHttps` default, no runtime Razor compilation in prod |
