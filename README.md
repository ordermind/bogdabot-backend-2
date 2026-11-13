# Bogdabot

Bogdabot is a .NET 10 backend for an automated trading system. It combines an ASP.NET Core API, an Akka.NET actor model, and database-backed services for account management, trading operations, and admin workflows.

The repository is organized into three projects:

- `server/` — the ASP.NET Core application and backend domain logic
- `shared/` — shared models, contracts, and cross-cutting types
- `cli/` — administrative command-line interface for maintenance tasks

## Project goals

- Run a versioned API under `/api/v1`
- Manage trader lifecycle and supervision through Akka actors
- Keep the main application data in PostgreSQL
- Use SQLite per trader instance for isolated trading data
- Expose admin operations through a simple CLI and a local Unix socket in development

## Architecture overview

The server follows a layered, domain-oriented structure under `server/src`:

- `Account` — account and authentication concerns
- `Infrastructure` — database, logging, and host bootstrapping
- `Trading` — trader actors, logic, and trading workflows
- `Web` — HTTP API and bootstrap setup
- `Shared` — shared configuration, value objects, and common infrastructure

The application bootstraps services in a specific order during startup and runs bootstrap actions before serving traffic.

## Prerequisites

- .NET 10 SDK
- PostgreSQL for the main database backend
- SQLite support for trader databases
- A configured `.env` file in `server/`

## Quick start

1. Copy the example environment file:

   ```bash
   cp server/.env.example server/.env
   ```

2. Edit `server/.env` and set the required values, including PostgreSQL credentials and any admin login details.

3. Restore dependencies and build the solution:

   ```bash
   dotnet restore bogdabot.slnx
   dotnet build bogdabot.slnx
   ```

4. Start the backend server:

   ```bash
   dotnet run --project server/Bogdabot.Server.csproj
   ```

5. In a separate terminal, use the admin CLI when needed:

   ```bash
   dotnet run --project cli/Bogdabot.Cli.csproj -- migrate-fresh-main
   dotnet run --project cli/Bogdabot.Cli.csproj -- migrate-fresh-traders
   ```

## Environment configuration

The application reads environment variables through the `server/.env` file. The repository includes a template at `server/.env.example` with the main settings:

- `DB_MAIN_BACKEND`
- `DB_MAIN_HOST`
- `DB_MAIN_PORT`
- `DB_MAIN_DATABASE`
- `DB_MAIN_USERNAME`
- `DB_MAIN_PASSWORD`
- `DB_TRADER_BACKEND`
- `DB_TRADER_IN_MEMORY`
- `DB_TRADER_ROOT_FOLDER`
- `SUPERADMIN_EMAIL`
- `SUPERADMIN_PASSWORD`
- `LOG_PATH`

## Running tests

The backend includes a dedicated tests project under `server/tests`.

```bash
dotnet test server/tests/tests.csproj
```

You can also filter specific test classes or names if you want to target a narrow subset.

## Admin commands

The CLI exposes the following commands:

- `migrate-fresh-main`
- `migrate-fresh-traders`

These are used to rebuild database schema state for the main and trader databases.

## API and development notes

- The server exposes versioned API routes under `/api/v1`
- Administrative commands are available through the Unix socket at `/tmp/bogdabot/admin-socket` in development
- Swagger and related web tooling are enabled through the ASP.NET Core server configuration

## Repository layout

```text
.
├── cli/
│   ├── AdminClient.cs
│   ├── AdminCommandLine.cs
│   ├── Bogdabot.Cli.csproj
│   └── Program.cs
├── server/
│   ├── .env.example
│   ├── Bogdabot.Server.csproj
│   ├── Program.cs
│   ├── src/
│   ├── tests/
│   └── ...
├── shared/
│   └── Bogdabot.Shared.csproj
├── bogdabot.slnx
├── LICENSE.txt
└── README.md
```

## License

This project is licensed under the MIT License. See [LICENSE.txt](LICENSE.txt) for details.
