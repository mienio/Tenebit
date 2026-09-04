# Tenebit — deploy: jak to działało na starym serwerze i co trzeba zrobić na nowym

Dokument opisowy. Nic tu nie jest uruchamiane automatycznie — to opis stanu faktycznego
starego środowiska (Mikrus) + lista wszystkiego, co trzeba odtworzyć na pustej maszynie.

---

## 0. Co leży w tym katalogu

`/home/ubuntu/Tenebit` = kopia 1:1 drzewa źródeł z maszyny deweloperskiej (`D:\Tenebit`),
razem z historią gita (`.git`, branch `feature/protokol-pdf-i-naprawy-audytu`).

Skopiowane:

- `Tenebit.Backend/` — solucja .NET (`Tenebit.sln`): `Tenebit.Api`, `Tenebit.Application`,
  `Tenebit.Domain`, `Tenebit.Infrastructure`, `Tenebit.Tests` + **`migrate.sql`** (kluczowy plik, patrz §5)
- `Tenebit.Frontend/` — React 18 + Vite 6 + TypeScript, `Dockerfile`, `nginx.conf`, testy `vitest` i `e2e` (Playwright)
- `deploy.sh` — skrypt deployu ze starego serwera (wersja aktualna, z poprawkami)
- `deployspec.txt` — oryginalna specyfikacja procesu deployu na Mikrusie
- `docker-compose.production.yml` — utwardzony compose (db + migrator + backend + frontend) — **to jest wzorzec dla nowego serwera**
- `compose.production.yml` — starszy wariant (bez usługi `db`), zostawiony historycznie, **nie używać**
- `releasescripts/` — `backup-db.sh`, `restore-db.sh`, `smoke-test.sh`
- `scripts/` — pakowanie źródeł i skrypty weryfikacyjne/audytowe
- `docs/`, `spec/`, `PRODUCT.md` — dokumentacja produktowa i architektura

**NIE skopiowane** (celowo, to artefakty budowania — odtwarzają się przy buildzie):
`Tenebit.Frontend/node_modules`, `Tenebit.Frontend/dist`, `**/bin`, `**/obj`, `*.tsbuildinfo`,
`Release/` (stare ZIP-y wydań), logi.

**Nie ma tu żadnych sekretów produkcyjnych** — nigdy nie były trzymane w repo.
Cała konfiguracja produkcyjna żyła w pliku `.env` i w `docker-compose.yml` **na serwerze**.
Wartości ze starego serwera (hasło do bazy, klucz JWT, klucz szyfrowania pól, dane SMTP)
trzeba przenieść ręcznie albo wygenerować od nowa (§6).

---

## 1. Stack i architektura

Trzy kontenery w jednym projekcie compose:

```
[ TLS / reverse proxy ]  ->  frontend (nginx, SPA + proxy /api/)  ->  backend (.NET, :8080)  ->  db (PostgreSQL)
```

- **backend** — ASP.NET Core, `net10.0`, wejście `Tenebit.Api.dll`, słucha na `:8080`.
  Health: `GET /api/health` (żywotność) i `GET /api/health/ready` (realny test połączenia z bazą,
  zwraca `{"status":"ready"}`).
- **frontend** — build Vite → statyczne pliki w nginx. nginx serwuje SPA i **proxuje `/api/` do backendu
  po nazwie usługi w sieci compose**. Dlatego `VITE_API_BASE_URL` jest **puste** w buildzie —
  front woła API ścieżką względną `/api/...`, nie ma CORS-u i domena nie jest zaszyta w bundle.
- **db** — PostgreSQL. Schema aplikacji: **`tenebit`** (nie `public`). Dane w wolumenie dockera.

Wersje z obrazów: `mcr.microsoft.com/dotnet/sdk:10.0` + `aspnet:10.0`, `node:22-alpine`,
`nginxinc/nginx-unprivileged:1.27-alpine`, `postgres:17-alpine`.

---

## 2. Jak wyglądał STARY serwer (Mikrus)

- VPS Mikrus, dostęp SSH na **roota** (`gabe178.mikrus.xyz:10178`, tylko hasło — klucze były odrzucane).
- Domena: darmowa poddomena Mikrusa **`https://skinny-tiger4900.byst.re`**.
  **TLS terminował Mikrus** w swoim reverse proxy (panel: Domeny/Usługi → proxy 443 → port lokalny).
  Do kontenera szedł zwykły HTTP. Na serwerze nie było certbota ani własnego nginx-a hosta.
  Docelowa domena to **`teneb.it`** — na starym serwerze nie była jeszcze przełączona.
- Katalog bazowy: **`/opt/tenebit`**
  ```
  /opt/tenebit/
    backend/            <- rozpakowane źródła backendu (podmieniane przy każdym deployu)
    frontend/           <- rozpakowane źródła frontendu (podmieniane przy każdym deployu)
    backups/            <- backup-y bazy (db-*.sql.gz) i katalogów (tar.gz)
    docker-compose.yml  <- TYLKO na serwerze, NIE w repo
    .env                <- sekrety, TYLKO na serwerze
    deploy.sh, backup-db.sh, restore-db.sh, smoke-test.sh
  ```
- Projekt compose: **`docker compose -p tenebit`**, kontenery: `tenebit-db`, `tenebit-backend`, `tenebit-frontend`.
- **Nazwa bazy na starym serwerze to `Tanebit`** (literówka z pierwszej instalacji), user `postgres`.
  Dlatego `deploy.sh`, `backup-db.sh` i `restore-db.sh` czytają `POSTGRES_DB` z `/opt/tenebit/.env`
  zamiast zakładać `tenebit` — wcześniej hardkod złej nazwy powodował, że `pg_dump` cicho nie robił nic.
  **Na nowym serwerze załóż bazę jako `tenebit`** i nie powielaj tej literówki.
- Frontendowy kontener nasłuchiwał na porcie **80** (wersja nginx-a generowana przez `deploy.sh`),
  i to na ten port mapował proxy Mikrusa.

---

## 3. Proces release — jak faktycznie wypuszczało się wersję

1. Lokalnie (Windows): spakować dwa ZIP-y:
   - `Tenebit.Backend.zip` — musi zawierać `Tenebit.sln` **maks. 4 poziomy zagnieżdżenia** (tak szuka `deploy.sh`)
   - `Tenebit.Frontend.zip` — musi zawierać `package.json`, tak samo maks. 4 poziomy
   - ZIP-y trzymane w `D:\Tenebit\Release\` (nie w korzeniu `D:\`)
   - **`migrate.sql` musi być w korzeniu ZIP-a backendu** — bez tego deploy przerywa (§5)
2. Wgrać oba ZIP-y na serwer do **`/root/`** (scp/WinSCP/panel).
3. Na serwerze: `bash /root/deploy.sh` (skrypt sam się nie uruchamia, nie ma CI/CD, nie ma webhooków).

`deploy.sh` krok po kroku:

1. Sprawdza obecność obu ZIP-ów w `/root/`; brak → `exit 1`.
2. `pg_dump` bazy → `backups/db-<stamp>.sql.gz` (błąd = tylko WARN, deploy leci dalej).
3. **Backend**: `tar.gz` backup obecnego `backend/` → unzip do `/tmp` → znalezienie katalogu z `.sln` →
   **guard: brak `migrate.sql` = STOP przed jakąkolwiek zmianą** (obejście: `ALLOW_MISSING_MIGRATIONS=1`) →
   `rm -rf backend/` i podmiana zawartości → **nadpisanie `backend/Dockerfile`** wersją z heredoca w skrypcie.
4. **Frontend**: backup → unzip → podmiana `frontend/` → **nadpisanie `frontend/Dockerfile` i `frontend/nginx.conf`**
   wersjami z heredoca w skrypcie.
5. `docker compose -p tenebit build backend` + `build frontend`.
6. **Migracja**: `docker exec -i tenebit-db psql -U postgres -d $POSTGRES_DB -v ON_ERROR_STOP=1 < backend/migrate.sql`
   — przed podniesieniem nowego backendu.
7. `docker compose -p tenebit up -d`.
8. Czekanie na backend w pętli (40 × 3 s) na `$DOMAIN/api/health` — API potrzebuje ~15 s na rozgrzanie
   (budowa modelu EF + pierwsze połączenie). Sztywny `sleep 5` dawał fałszywe FAIL-e.
9. Weryfikacja: listing plików w kontenerze frontu, `curl $DOMAIN/api/health` musi zawierać `ok`,
   `curl $DOMAIN/` musi zawierać marker aplikacji; retry 10×, bo w trakcie podmiany nginx-a
   pojedynczy strzał potrafił trafić w okno przełączenia.
10. **Smoke test**: `/api/health/ready` musi zwrócić `"status":"ready"`, potem realna rejestracja konta
    `smoketest+<ts>@tenebit-internal.test`. Uwaga: rejestracja wymaga `acceptTerms: true`, a przy włączonym
    SMTP zwraca **202 + `requiresEmailVerification`**, nie token — dlatego skrypt nie robi już
    pełnego roundtripu register→login.
11. **Sprzątanie po smoke teście**: SQL usuwający organizację `Smoke Test <ts>` i wszystkie wiersze
    z `OrganizationId` w schemacie `tenebit` (6 przebiegów, żeby ominąć kolejność FK). Bez tego każdy
    deploy zostawiał śmieć w bazie.
12. `rm -rf` katalogów tymczasowych, `docker compose ps`.

Brak automatycznego rollbacku. Powrót = ręcznie z `backups/` (§7).

---

## 4. Pułapka: `deploy.sh` nadpisuje pliki z repo

`deploy.sh` **generuje własne** `backend/Dockerfile`, `frontend/Dockerfile` i `frontend/nginx.conf`
z heredoców w skrypcie. Wersje z repo **nigdy nie trafiały na produkcję**. Konsekwencje:

- Nagłówki bezpieczeństwa z repo (`Content-Security-Policy`, HSTS, `X-Frame-Options`, rate limit
  `limit_req`, `access_log off`, `/healthz`) **nie działały na produkcji** — produkcyjny nginx był
  ubogą wersją ze skryptu.
- Repo-wy front nasłuchuje na **8080** i działa jako `nginx-unprivileged` (user 101);
  wersja z `deploy.sh` to zwykły `nginx:alpine` na **80**.
- Repo-wy backendowy `Dockerfile` jest utwardzony (`USER app`, `read_only`, `DOTNET_EnableDiagnostics=0`);
  wersja ze skryptu — nie.

**Na nowym serwerze:** używaj plików z repo (`Tenebit.Backend/Dockerfile`, `Tenebit.Frontend/Dockerfile`,
`Tenebit.Frontend/nginx.conf`) i `docker-compose.production.yml`, a `deploy.sh` traktuj jako opis
historii, nie jako narzędzie do skopiowania 1:1.

Dwa błędy, które wynikały wprost z tych heredoców i są już naprawione — nie cofnij ich:

- **NODE_ENV**: w buildzie frontu było `ENV NODE_ENV=development` (potrzebne, żeby zainstalowały się
  devDependencies), ale bez `NODE_ENV=production` przy samym `vite build` na produkcję szedł
  **developerski bundle Reacta**. Poprawka: `RUN ./node_modules/.bin/tsc -b && NODE_ENV=production ./node_modules/.bin/vite build`.
- **`/assets` 403**: aplikacja ma trasę SPA `/assets`, a Vite generuje katalog `assets/`.
  Blokowa reguła `location /assets/` w nginx-ie przechwytywała wejście na `https://domena/assets`
  i zwracała 403. Poprawka: regułę zawęzić do plików z rozszerzeniem
  (`location ~* ^/assets/.+\.[A-Za-z0-9]+$`), a resztę zostawić SPA fallbackowi.
- **Healthcheck a `AllowedHosts`**: backend odrzuca żądanie bez pasującego nagłówka `Host` (400),
  więc healthcheck w heredocu musiał wysyłać `-H "Host: <domena>"`, inaczej kontener był
  raportowany jako `unhealthy` mimo działającego API. Wersja z repo woła `127.0.0.1` i przechodzi,
  bo `AllowedHosts` w produkcji ustawiasz sam — jeśli zawęzisz je tylko do `teneb.it`, healthcheck
  po `127.0.0.1` zacznie zwracać 400. Dopisz wtedy `localhost` do `AllowedHosts`.

---

## 5. Migracje bazy — NAJWAŻNIEJSZE

- Schema aplikacji: **`tenebit`**. Historia migracji: tabela `__EFMigrationsHistory`.
- **`dotnet ef migrations add` w tym repo NIE DZIAŁA** (rozjechany snapshot modelu).
  Nowe migracje pisze się **ręcznie jako SQL** i dokleja do `Tenebit.Backend/migrate.sql`.
- **`Tenebit.Backend/migrate.sql`** (~170 KB) to **idempotentny, kumulatywny** skrypt całego schematu
  (odpowiednik `dotnet ef migrations script --idempotent`). Każdy blok jest owinięty warunkiem
  „jeśli tej migracji nie ma w `__EFMigrationsHistory`”, więc:
  - na **pustej bazie** zbuduje cały schemat od zera,
  - na istniejącej bazie dołoży tylko brakujące migracje,
  - puchnie w nieskończoność, ale jest bezpieczny do wielokrotnego uruchamiania.
- Wydanie bez zmian schematu i tak **musi** zawierać ten sam `migrate.sql` co poprzednie.
  „Tym razem nie ma migracji” nigdy nie jest powodem, żeby go nie było — brak pliku oznacza,
  że krok pakowania go zgubił. Dlatego `deploy.sh` przerywa **przed** dotknięciem czegokolwiek na serwerze.
  To zabezpieczenie powstało po incydencie 2026-08-14 (`relation does not exist` — kod poszedł na produkcję
  przed schematem).
- Aplikacja **nie migruje się sama**: `Database__AutoCreate=false`, `Seed__Enabled=false`.
  Obraz runtime to `aspnet`, a nie `sdk`, więc **w kontenerze nie ma `dotnet ef`**.

Dwie drogi zaaplikowania migracji:

1. **Stara (deploy.sh, sprawdzona)** — `psql` z pliku:
   ```bash
   docker exec -i tenebit-db psql -U postgres -d tenebit -v ON_ERROR_STOP=1 < backend/migrate.sql
   ```
   `ON_ERROR_STOP=1` jest obowiązkowe — bez tego błąd w środku skryptu przechodzi bez echa,
   a kod startuje na schemacie, którego nie rozumie.
2. **Nowa (`docker-compose.production.yml`)** — usługa `migrator`: ten sam obraz backendu odpalony raz
   komendą `dotnet Tenebit.Api.dll --migrate-only`, `restart: no`, a `backend` startuje dopiero
   przy `service_completed_successfully`. Backend obsługuje flagę `--migrate-only` (Program.cs).
   Ta droga nie potrzebuje `migrate.sql` w ogóle, bo używa migracji EF wkompilowanych w obraz.

**Na pustej bazie wybierz jedną i się jej trzymaj.** Mieszanie ich jest bezpieczne tylko o tyle, o ile
obie zapisują tę samą historię do `__EFMigrationsHistory` — a `migrate.sql` bywa ręcznie dopisywany,
więc rozjazd jest realny. Rekomendacja dla nowego serwera: **`migrator`** (droga 2), bo eliminuje
ręczne pakowanie `migrate.sql`; `migrate.sql` zostaje jako awaryjny sposób odtworzenia schematu.

---

## 6. Sekrety i zmienne środowiskowe

Nigdy nie były w repo. Na starym serwerze: `/opt/tenebit/.env` + wpisy `environment:` w
`docker-compose.yml` na serwerze. `docker-compose.production.yml` z repo czyta je z `.env`
obok pliku compose i **wymusza obecność** (składnia `${VAR:?required}` — brak zmiennej = compose nie wystartuje).

Wymagane:

| Zmienna | Znaczenie |
|---|---|
| `TENEBIT_DB_PASSWORD` | hasło użytkownika `tenebit` w Postgresie (compose zakłada bazę `tenebit`/user `tenebit`) |
| `TENEBIT_SIGNING_KEY` | klucz podpisu JWT, min. 32 znaki |
| `TENEBIT_FIELD_ENCRYPTION_KEY` | klucz szyfrowania pól w bazie (`Auth__FieldEncryption__Keys__primary`) — **utrata = utrata danych zaszyfrowanych tym kluczem** |
| `TENEBIT_PUBLIC_URL` | `https://teneb.it` — używane w mailach, linkach QR, CORS |
| `TENEBIT_ALLOWED_HOSTS` | np. `teneb.it,localhost` (patrz uwaga o healthchecku w §4) |
| `TENEBIT_SMTP_HOST`, `TENEBIT_SMTP_FROM` | wymagane; `Email__Enabled=true` w produkcji |

Opcjonalne (mają wartości domyślne): `TENEBIT_SMTP_PORT` (587), `TENEBIT_SMTP_USERNAME`,
`TENEBIT_SMTP_PASSWORD`, `TENEBIT_SMTP_FROM_NAME` (Tenebit), `TENEBIT_SMTP_USE_SSL` (true),
`TENEBIT_HTTP_PORT` (8080), `TENEBIT_IMAGE_TAG` (local).

Stripe i logowanie OAuth (Google/Microsoft/Facebook/Apple) są w `appsettings.json` puste —
jeśli mają działać, dołóż `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Stripe__Prices__*`
oraz `Auth__OAuth__*` jako zmienne środowiskowe backendu.

Generowanie kluczy:
```bash
openssl rand -base64 48   # TENEBIT_SIGNING_KEY
openssl rand -base64 32   # TENEBIT_FIELD_ENCRYPTION_KEY
openssl rand -base64 24   # TENEBIT_DB_PASSWORD
```

`Auth__FieldEncryption__AllowLegacyPlaintext` **musi zostać `false`** na produkcji.

---

## 7. Backup, restore, smoke test

- **`releasescripts/backup-db.sh`** — `pg_dump | gzip` do `$BASE/backups/db/`, kasuje starsze niż
  `TENEBIT_BACKUP_RETENTION_DAYS` (domyślnie 14). Cron na starym serwerze:
  ```
  0 3 * * * /opt/tenebit/backup-db.sh >> /var/log/tenebit-backup.log 2>&1
  ```
- **`releasescripts/restore-db.sh <plik.sql.gz> [--force]`** — pyta o potwierdzenie słowem `tak`,
  zatrzymuje `tenebit-backend` na czas restore, potem go wznawia.
- **`releasescripts/smoke-test.sh <domena>`** — **UWAGA: nieaktualny.** Wysyła rejestrację bez
  `acceptTerms` i oczekuje tokenu w odpowiedzi. Aktualna wersja tego testu jest w `deploy.sh`
  (`acceptTerms: true`, oczekiwane `requiresEmailVerification`). Przy przenoszeniu zaktualizuj skrypt
  albo używaj wersji z `deploy.sh`.
- Wszystkie trzy skrypty mają zaszyte `BASE=/opt/tenebit` i nazwy kontenerów `tenebit-db` /
  `tenebit-backend` — przy zmianie układu katalogów na nowym serwerze trzeba je poprawić.

---

## 8. Uruchomienie na NOWYM, pustym serwerze — od zera

Serwer docelowy: `ubuntu@teneb.it`, user `ubuntu` (nie root — wszystko przez `sudo`),
Ubuntu, Docker jest zainstalowany, nie ma Postgresa ani niczego z aplikacji.

1. **Docker dla usera `ubuntu`** (żeby nie klikać `sudo` przy każdym `docker`):
   `sudo usermod -aG docker ubuntu`, wylogować się i zalogować ponownie.
   Sprawdzić: `docker compose version` (musi być plugin `compose`, nie stare `docker-compose`).
2. **Katalog roboczy** — kod jest już w `/home/ubuntu/Tenebit`. Compose z repo
   (`docker-compose.production.yml`) buduje obrazy z kontekstów `./Tenebit.Backend` i `./Tenebit.Frontend`,
   więc uruchamiaj go **z tego katalogu**.
3. **`.env`** obok pliku compose (`/home/ubuntu/Tenebit/.env`), z kompletem zmiennych z §6.
   `chmod 600 .env`. Do repo nie trafi — `.gitignore` już to blokuje.
4. **Build i start:**
   ```bash
   cd /home/ubuntu/Tenebit
   docker compose -f docker-compose.production.yml build
   docker compose -f docker-compose.production.yml up -d
   ```
   Kolejność jest wymuszona w pliku: `db` (healthcheck `pg_isready`) → `migrator` (`--migrate-only`,
   kończy się i znika) → `backend` (healthcheck) → `frontend`.
   Uwaga: build frontu odpala `npm run lint -- --max-warnings 0 && npm test` — czerwony lint albo
   test **przerywa build obrazu**. To celowe.
5. **Awaryjnie, gdyby `migrator` nie przeszedł** — schemat z pliku:
   ```bash
   docker compose -f docker-compose.production.yml exec -T db \
     psql -U tenebit -d tenebit -v ON_ERROR_STOP=1 < Tenebit.Backend/migrate.sql
   ```
6. **TLS / wejście z internetu.** Frontend jest publikowany na **`127.0.0.1:8080`** (tylko loopback),
   nasłuchuje w kontenerze na 8080 i sam **nie robi TLS-a** — na starym serwerze certyfikat robił
   Mikrus. Tu trzeba postawić własny terminator TLS na `teneb.it`, który przekaże ruch na
   `127.0.0.1:8080`. Wymagania od strony aplikacji:
   - proxy musi ustawiać `X-Forwarded-Proto: https` i `X-Forwarded-For`,
   - `ReverseProxy__KnownProxies__0` w compose wskazuje `172.30.0.10` (kontener frontu) —
     jeśli wstawisz przed frontem jeszcze jeden proxy na hoście, upewnij się, że backend widzi
     poprawny łańcuch, inaczej rate limiting i logi zobaczą IP proxy zamiast klienta,
   - `TENEBIT_ALLOWED_HOSTS` musi zawierać `teneb.it`,
   - `TENEBIT_PUBLIC_URL=https://teneb.it` — z tego generują się linki w mailach i **kody QR**
     (format linku QR jest celowo wielkimi literami).
   - firewall: na zewnątrz otwarte tylko 80/443 (+22). Port 8080 zostaje na loopbacku.
7. **DNS** `teneb.it` → IP nowego serwera (na starym środowisku domena nie była jeszcze przełączona).
8. **Weryfikacja po starcie:**
   ```bash
   docker compose -f docker-compose.production.yml ps          # wszystko healthy, migrator = exited (0)
   curl -fsS https://teneb.it/api/health                        # zawiera "ok"
   curl -fsS https://teneb.it/api/health/ready                  # {"status":"ready"} = baza odpowiada
   curl -fsS https://teneb.it/healthz                           # "ok" z nginx-a
   curl -fsS https://teneb.it/ | head -20                       # DOCTYPE + assets/index...
   ```
   Backend potrzebuje ~15 s na rozgrzanie po starcie — nie panikuj przy pierwszym `curl`.
9. **Cron na backup** (§7), po dostosowaniu ścieżek i nazw kontenerów.
10. **Przeniesienie danych ze starego serwera** (jeśli produkcja ma zachować dane):
    na starym `pg_dump` bazy **`Tanebit`**, na nowym `psql` do bazy `tenebit` —
    schemat wewnątrz dumpa to `tenebit`, więc nazwa bazy może się różnić bez konsekwencji.
    Razem z bazą **musi** przyjechać `TENEBIT_FIELD_ENCRYPTION_KEY` ze starego `.env` —
    inaczej zaszyfrowane pola będą nieodczytywalne.

---

## 9. Nazewnictwo: stary układ vs. repo

| | stary serwer (`deploy.sh`) | repo (`docker-compose.production.yml`) |
|---|---|---|
| katalog | `/opt/tenebit` | dowolny (tu `/home/ubuntu/Tenebit`) |
| projekt compose | `-p tenebit` | `name: tenebit` w pliku |
| kontenery | `tenebit-db`, `tenebit-backend`, `tenebit-frontend` | `tenebit-db-1`, `tenebit-backend-1`, `tenebit-frontend-1` (domyślne nazwy compose) |
| baza / user | `Tanebit` / `postgres` | `tenebit` / `tenebit` |
| port frontu | 80 (publiczny, proxy Mikrusa) | 8080 na `127.0.0.1` |
| migracje | `psql < migrate.sql` z `deploy.sh` | usługa `migrator` (`--migrate-only`) |
| sieć | domyślna sieć compose | `172.30.0.0/24`, statyczne IP |

**Skrypty z `releasescripts/` i `deploy.sh` używają starych nazw kontenerów** — pod nowym compose
trzeba je poprawić albo wywoływać przez `docker compose ... exec`.

---

## 10. Skróty — pliki, do których warto zajrzeć

- `deployspec.txt` — oryginalna, pełna specyfikacja procesu na Mikrusie (po polsku)
- `deploy.sh` — działający skrypt starego deployu, z komentarzami tłumaczącymi każdą poprawkę
- `docker-compose.production.yml` — docelowy układ na nowy serwer
- `Tenebit.Backend/migrate.sql` — pełny, idempotentny schemat bazy
- `Tenebit.Frontend/nginx.conf` — nginx z nagłówkami bezpieczeństwa (ten, który ma iść na produkcję)
- `Tenebit.Frontend/.env.example` — zmienne buildu frontu (m.in. dane operatora do stron prawnych)
- `scripts/verify-release-artifact.sh` — sprawdza, czy paczka źródeł nie zawiera sekretów
- `PRODUCT.md`, `docs/implementation/ARCHITECTURE.md` — kontekst produktowy i architektura
