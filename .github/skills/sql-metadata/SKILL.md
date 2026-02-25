---
name: sql-metadata
description: Introspect SQL Server databases using the MediR CLI. List tables or retrieve column-level schema. Use when you need to discover database structure, inspect table shapes, or verify schema before generating queries, migrations, entities, or data models.
argument-hint: "[database] [table?]"
context: fork
---

# SQL Metadata

Discover SQL Server database structure using `medircli`. Both commands authenticate via **Microsoft Entra ID (Active Directory Interactive / MFA)** — no passwords or connection strings required. Tokens are cached after the first prompt.

Default server: `mediroutesdev.database.windows.net`

## Commands

| Command | Use For |
|---------|---------|
| `medircli sql list-tables` | List all base tables in a database |
| `medircli sql get-table-schema` | Get column definitions for a specific table |

## When to Use

- **Discover available tables** — before writing queries or generating models
- **Inspect a table's shape** — column names, types, nullability, defaults
- **Pre-migration analysis** — understand existing schema before generating migrations
- **Agent automation** — dynamically discover database structure without hardcoding it

## Usage

### List Tables

Returns all `BASE TABLE` names sorted alphabetically.

```bash
medircli sql list-tables --server <server> --database <database>
```

| Flag | Short | Required | Description |
|------|-------|----------|-------------|
| `--server` | `-s` | Yes | SQL Server hostname (e.g. `yourserver.database.windows.net`) |
| `--database` | `-d` | Yes | Name of the database to query |

**Example:**
```bash
medircli sql list-tables -s mediroutesdev.database.windows.net -d MyDatabase
```

**Output:** Newline-separated list of table names.

---

### Get Table Schema

Returns column definitions ordered by ordinal position.

```bash
medircli sql get-table-schema --server <server> --database <database> --table <table>
```

| Flag | Short | Required | Description |
|------|-------|----------|-------------|
| `--server` | `-s` | Yes | SQL Server hostname (e.g. `yourserver.database.windows.net`) |
| `--database` | `-d` | Yes | Name of the database to query |
| `--table` | `-t` | Yes | Table name to retrieve the schema for |

**Example:**
```bash
medircli sql get-table-schema -s mediroutesdev.database.windows.net -d MyDatabase -t Users
```

**Output:** Formatted table with columns: `TABLE_NAME`, `COLUMN_NAME`, `DATA_TYPE`, `IS_NULLABLE`, `COLUMN_DEFAULT`.

## Workflow

Use `list-tables` first to discover available tables, then `get-table-schema` to drill into a specific one:

```bash
# Step 1: discover what tables exist
medircli sql list-tables -s mediroutesdev.database.windows.net -d MyDatabase

# Step 2: inspect a specific table
medircli sql get-table-schema -s mediroutesdev.database.windows.net -d MyDatabase -t Users
```
