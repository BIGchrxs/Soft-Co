# Soft & Co. order tracker

ASP.NET Core MVC order tracking with PostgreSQL and role-based access. The interface follows the supplied Soft & Co. reference: cream navigation, brown accents, light surfaces and compact order tables.

## Order workflow

- Track suppliers, products, linked projects, cargo readiness, invoices and delivery stages.
- Search by supplier, product or invoice reference; combine supplier, project, delivery and settlement filters.
- Expand payment columns when reconciling an order. Summary amounts are in ZAR; individual foreign values retain their currency.
- Open an order to review delivery progress, edit its details and record payments using the existing role permissions.
- Open supplier and project names to see their linked orders.
- Missing project assignments, invoice values and cargo dates have explicit labels.

## Workbook reference

`International Payment Tracker.xlsx` was read as reference data only. Its Suppliers sheet contains 29 order lines and its Service Provider sheet contains 2 freight lines. The existing development-only `TrackerSeed` already represents these entries and only seeds an empty orders table. The original workbook is not changed, and this update does not import into or overwrite an existing database.

The workbook fields map to supplier/product, linked projects, delivery status, cargo readiness, invoice date/reference, Rand and foreign invoice values, deposits and settlements. The existing seed infers currencies from the Rand/foreign ratio, assumes 2026 for day/month dates and makes payment/status assumptions. In particular, some workbook rows say Outstanding even though their deposit and settlement columns total the invoice. Those values need business confirmation before treating the seed as production financial records. This design update preserves the existing seed and calculation rules.

Shared orders appear under each linked project; project balances are therefore not additive. Summary cards on filtered pages reflect only the displayed results. These are operational views, not a live spreadsheet sync.

## Run locally

Requires the .NET 10 SDK and PostgreSQL. Configure `ConnectionStrings__DefaultConnection`, `Seed__AdminEmail` and `Seed__AdminPassword` for your local environment, then run:

```powershell
dotnet run --project SoftCo/SoftCo.csproj --launch-profile https
```

Use HTTPS at `https://localhost:7027` because authentication cookies require HTTPS. Start the local database with `docker compose -p softco up -d softco-db`. SoftCo uses host port **5433**, so another project's PostgreSQL can continue using 5432. The development fallback connects to `localhost:5433`; the Docker web service connects to `softco-db:5432` inside its network. The existing named database volume is retained when the container is recreated. Migrations run at startup. Never use a production database for browser write tests.

If you set `ConnectionStrings:DefaultConnection` in .NET user secrets or `ConnectionStrings__DefaultConnection` in your environment, it overrides the development fallback: use port 5433 and the credentials belonging to the SoftCo database. Changing `POSTGRES_PASSWORD` in `.env` does not change a password already stored in a PostgreSQL volume.

## Verification

```powershell
dotnet build SoftCo/SoftCo.csproj
```

`tests/order-tracker.smoke.cjs` runs real browser checks through login, search, filtering, empty results, payment-column expansion, supplier/project navigation and mobile layout. It requires Playwright with Microsoft Edge and a disposable database populated with the development seed.

Set `TRACKER_TEST_URL` (defaults to `https://localhost:7027`), `TRACKER_TEST_EMAIL` and `TRACKER_TEST_PASSWORD` through your environment. Set `PLAYWRIGHT_MODULE` to an absolute Playwright module directory if it is not installed on the Node module path. Then run:

```powershell
node tests/order-tracker.smoke.cjs
```

Opt into create/edit/payment checks with `TRACKER_TEST_WRITES=1` only for a disposable test database. This adds one test order and payment. Screenshots are written to the gitignored `artifacts/` directory.
