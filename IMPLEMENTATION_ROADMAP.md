# Enterprise MS SQL MCP Server — Complete Implementation Roadmap

## Document Analysis Summary

**Project:** Production-Grade MCP Server for Microsoft SQL Server using .NET  
**Stack:** .NET 8, C#, ASP.NET Core, MCP SDK, Dapper, ADO.NET, Serilog, OpenTelemetry, Polly  
**Architecture:** Clean Architecture + DDD + SOLID + 5-Layer Design  
**Deliverables:** 20 items — source code, Docker, K8s, CI/CD, guides

---

## Solution Architecture

```
SqlMcpServer/
├── src/
│   ├── SqlMcpServer.Host/                  # Presentation Layer — MCP Server entry point
│   ├── SqlMcpServer.Application/           # Application Layer — Business Services
│   ├── SqlMcpServer.Domain/                # Domain Layer — Entities, Models, Contracts
│   ├── SqlMcpServer.Infrastructure/        # Infrastructure Layer — SQL, Repos, Cache
│   └── SqlMcpServer.CrossCutting/          # Cross-Cutting — Logging, Security, Validation
├── deploy/
│   ├── docker/
│   ├── kubernetes/
│   ├── azure/
│   └── github-actions/
├── docs/
│   ├── architecture/
│   ├── deployment/
│   ├── operations/
│   └── troubleshooting/
├── SqlMcpServer.sln
├── Dockerfile
├── docker-compose.yml
└── README.md
```

---

## Phase 0 — Prerequisites & Environment Setup

### Milestone 0.1 — Developer Workstation
- [ ] Install .NET 8 SDK
- [ ] Install Visual Studio 2022 or VS Code with C# Dev Kit
- [ ] Install Docker Desktop
- [ ] Install SQL Server (Developer Edition) or connect to existing
- [ ] Install Claude Desktop (for MCP testing)
- [ ] Install Azure CLI (for Azure deployment steps)

### Milestone 0.2 — Repository Initialization
- [ ] Create Git repository
- [ ] Add `.gitignore` (VisualStudio template + secrets exclusions)
- [ ] Add `.editorconfig` for code style enforcement
- [ ] Add `Directory.Build.props` for centralized NuGet versioning
- [ ] Add `global.json` pinning .NET 8 SDK version
- [ ] Create solution file: `dotnet new sln -n SqlMcpServer`
- [ ] Define NuGet package versions centrally in `Directory.Packages.props`

### Milestone 0.3 — NuGet Package Inventory
Packages needed across all projects:
- `ModelContextProtocol` (official .NET MCP SDK)
- `Microsoft.Data.SqlClient`
- `Dapper`
- `FluentValidation`
- `Serilog`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`, `Serilog.Sinks.Seq`
- `OpenTelemetry`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.SqlClient`
- `Polly`, `Microsoft.Extensions.Http.Polly`
- `Microsoft.Extensions.Caching.Memory`, `Microsoft.Extensions.Caching.StackExchangeRedis`
- `Azure.Identity`, `Azure.Security.KeyVault.Secrets`

---

## Phase 1 — Domain Layer (`SqlMcpServer.Domain`)

### Milestone 1.1 — Project Scaffold
- [ ] `dotnet new classlib -n SqlMcpServer.Domain`
- [ ] No external dependencies (pure domain — no NuGet references except system libraries)

### Milestone 1.2 — Database Metadata Entities
Define entities representing SQL Server objects:

- [ ] `DatabaseInfo` { Name, State, CompatibilityLevel, Collation, CreateDate }
- [ ] `SchemaInfo` { Name, Owner, SchemaId }
- [ ] `TableInfo` { Schema, Name, Type, RowCount, CreateDate, ModifyDate, HasClusteredIndex }
- [ ] `ColumnInfo` { TableSchema, TableName, ColumnName, OrdinalPosition, DataType, MaxLength, Precision, Scale, IsNullable, HasDefault, DefaultValue, IsComputed, IsIdentity }
- [ ] `ViewInfo` { Schema, Name, Definition, IsUpdatable, CheckOption }
- [ ] `FunctionInfo` { Schema, Name, Type (Scalar/TVF/ITVF), ReturnType, Definition, Parameters }
- [ ] `ProcedureInfo` { Schema, Name, Definition, Parameters, CreateDate, ModifyDate }
- [ ] `TriggerInfo` { Schema, Name, ParentTable, IsEnabled, TriggerType, Events, Definition }
- [ ] `IndexInfo` { Schema, Table, Name, Type, IsUnique, IsPrimaryKey, Columns, IncludedColumns, FillFactor }
- [ ] `ForeignKeyInfo` { Schema, Table, Name, Columns, ReferencedSchema, ReferencedTable, ReferencedColumns, DeleteAction, UpdateAction }
- [ ] `ConstraintInfo` { Schema, Table, Name, Type, Definition, Columns }
- [ ] `UserDefinedTypeInfo` { Schema, Name, BaseType, MaxLength, IsNullable, IsTableType }
- [ ] `TableTypeInfo` { Schema, Name, Columns }
- [ ] `SequenceInfo` { Schema, Name, DataType, StartValue, Increment, MinValue, MaxValue, IsCycling }
- [ ] `SynonymInfo` { Schema, Name, BaseObject }
- [ ] `DependencyInfo` { ObjectSchema, ObjectName, ObjectType, ReferencedSchema, ReferencedName, ReferencedType }
- [ ] `ExecutionPlanInfo` { QueryText, PlanXml, EstimatedCost, StatementType }
- [ ] `QueryResult` { Columns, Rows, RowCount, ExecutionTimeMs, AffectedRows }
- [ ] `TableStatistics` { TableName, RowCount, ReservedKB, DataKB, IndexKB, UnusedKB, LastUpdated }

### Milestone 1.3 — Domain Exceptions
- [ ] `McpDomainException` (base)
- [ ] `ValidationException : McpDomainException`
- [ ] `DatabaseException : McpDomainException`
- [ ] `SecurityException : McpDomainException`
- [ ] `ToolException : McpDomainException`
- [ ] `TransientException : McpDomainException`
- [ ] `ObjectNotFoundException : McpDomainException`
- [ ] `QuerySafetyViolationException : SecurityException`

### Milestone 1.4 — Contracts (Interfaces)
- [ ] `ISchemaRepository` — schema-level queries
- [ ] `ITableRepository` — table metadata
- [ ] `IViewRepository` — view metadata
- [ ] `IFunctionRepository` — function metadata
- [ ] `IProcedureRepository` — stored procedure metadata
- [ ] `ITriggerRepository` — trigger metadata
- [ ] `ITypeRepository` — UDT and table types
- [ ] `IIndexRepository` — index metadata
- [ ] `IConstraintRepository` — FK and constraints
- [ ] `IQueryExecutor` — safe query execution
- [ ] `IDependencyRepository` — dependency analysis
- [ ] `IDocumentationService` — doc generation
- [ ] `ISchemaComparisonService` — schema diff
- [ ] `IQuerySafetyValidator` — SQL safety checks
- [ ] `ICacheService` — caching abstraction
- [ ] `IAuditLogger` — audit trail
- [ ] `ISecretProvider` — secrets abstraction
- [ ] `IConnectionFactory` — connection management

### Milestone 1.5 — Value Objects & Enums
- [ ] `SchemaObjectName` value object { Schema, Name } with formatting
- [ ] `ConnectionAuthMode` enum { WindowsAuth, SqlAuth, AzureManagedIdentity, AzureAD }
- [ ] `UserRole` enum { Admin, DBA, Developer, ReadOnly, Auditor }
- [ ] `ObjectType` enum { Table, View, Procedure, ScalarFunction, TVF, InlineTVF, Trigger, UDT, TableType }
- [ ] `IndexType` enum { Clustered, NonClustered, ColumnStore, XML, Spatial, FullText }
- [ ] `QuerySafetyLevel` enum { ReadOnly, ReadWrite, AdminOnly }

---

## Phase 2 — Infrastructure Layer (`SqlMcpServer.Infrastructure`)

### Milestone 2.1 — Project Scaffold
- [ ] `dotnet new classlib -n SqlMcpServer.Infrastructure`
- [ ] Reference `SqlMcpServer.Domain`
- [ ] Add NuGet: `Microsoft.Data.SqlClient`, `Dapper`, `Microsoft.Extensions.Caching.Memory`, `Azure.Identity`, `Azure.Security.KeyVault.Secrets`, `Polly`

### Milestone 2.2 — Connection Management
- [ ] `SqlConnectionFactory` implementing `IConnectionFactory`
  - [ ] Support Windows Authentication connection string
  - [ ] Support SQL Authentication connection string
  - [ ] Support Azure Managed Identity token provider
  - [ ] Support Azure AD Interactive/Password authentication
  - [ ] Connection pooling configuration (min/max pool size, connection lifetime)
  - [ ] Async `OpenAsync()` with cancellation token support
  - [ ] Connection health check method

### Milestone 2.3 — Repository Implementations (using Dapper)

For each repository, implement async methods with:
- CancellationToken support
- Dapper `QueryAsync` / `QueryFirstOrDefaultAsync`
- Proper `using` disposal of connections
- Parameter binding (no string interpolation)

**SchemaRepository**
- [ ] `GetDatabasesAsync()` — query `sys.databases`
- [ ] `GetSchemasAsync(database)` — query `sys.schemas`

**TableRepository**
- [ ] `GetTablesAsync(database, schema)` — `sys.tables` + `INFORMATION_SCHEMA.TABLES`
- [ ] `DescribeTableAsync(schema, table)` — full column metadata from `sys.columns`, `sys.types`
- [ ] `GetTableColumnsAsync(schema, table)` — `INFORMATION_SCHEMA.COLUMNS`
- [ ] `GetPrimaryKeysAsync(schema, table)` — `sys.key_constraints` + `sys.index_columns`
- [ ] `GetTableRelationshipsAsync(schema, table)` — `sys.foreign_keys`
- [ ] `GetTableStatisticsAsync(schema, table)` — `sp_spaceused` or `sys.dm_db_partition_stats`
- [ ] `GetRowCountAsync(schema, table)`
- [ ] `SampleTableDataAsync(schema, table, rowCount)` — TOP N with parameterized query
- [ ] `SearchTableDataAsync(schema, table, searchTerm, columns)` — safe parameterized search

**ViewRepository**
- [ ] `GetViewsAsync(database, schema)` — `sys.views`
- [ ] `DescribeViewAsync(schema, view)` — definition + columns
- [ ] `GetViewDefinitionAsync(schema, view)` — `sys.sql_modules`
- [ ] `GetViewDependenciesAsync(schema, view)` — `sys.sql_expression_dependencies`
- [ ] `GetViewColumnsAsync(schema, view)` — `INFORMATION_SCHEMA.VIEW_COLUMN_USAGE`

**FunctionRepository**
- [ ] `GetFunctionsAsync(database, schema)` — `sys.objects` WHERE type IN ('FN','IF','TF')
- [ ] `GetScalarFunctionsAsync(schema)` — type = 'FN'
- [ ] `GetTableValuedFunctionsAsync(schema)` — type IN ('IF','TF')
- [ ] `DescribeFunctionAsync(schema, name)` — parameters + return type
- [ ] `GetFunctionDefinitionAsync(schema, name)` — `sys.sql_modules`
- [ ] `AnalyzeFunctionDependenciesAsync(schema, name)` — `sys.sql_expression_dependencies`

**ProcedureRepository**
- [ ] `GetProceduresAsync(database, schema)` — `sys.procedures`
- [ ] `DescribeProcedureAsync(schema, name)` — definition + parameters
- [ ] `GetProcedureDefinitionAsync(schema, name)` — `sys.sql_modules`
- [ ] `GetProcedureParametersAsync(schema, name)` — `sys.parameters` + `sys.types`
- [ ] `AnalyzeProcedureDependenciesAsync(schema, name)` — `sys.sql_expression_dependencies`

**TriggerRepository**
- [ ] `GetTriggersAsync(database, schema)` — `sys.triggers`
- [ ] `DescribeTriggerAsync(schema, name)` — parent table, events, enabled status
- [ ] `GetTriggerDefinitionAsync(schema, name)` — `sys.sql_modules`
- [ ] `GetTriggerDependenciesAsync(schema, name)` — `sys.sql_expression_dependencies`

**TypeRepository**
- [ ] `GetUserDefinedTypesAsync(schema)` — `sys.types` WHERE is_user_defined = 1
- [ ] `GetTableTypesAsync(schema)` — `sys.table_types`
- [ ] `DescribeUserDefinedTypeAsync(schema, name)`
- [ ] `DescribeTableTypeAsync(schema, name)` — columns from `sys.columns`
- [ ] `GetTypeDefinitionAsync(schema, name)`

**IndexRepository**
- [ ] `GetIndexesAsync(schema, table)` — `sys.indexes` + `sys.index_columns`
- [ ] `GetMissingIndexesAsync()` — `sys.dm_db_missing_index_details`

**ConstraintRepository**
- [ ] `GetForeignKeysAsync(schema, table)` — `sys.foreign_keys` + `sys.foreign_key_columns`
- [ ] `GetConstraintsAsync(schema, table)` — check, default, unique constraints
- [ ] `GetSequencesAsync(schema)` — `sys.sequences`
- [ ] `GetSynonymsAsync(schema)` — `sys.synonyms`

**DependencyRepository**
- [ ] `FindObjectDependenciesAsync(schema, name)` — `sys.sql_expression_dependencies`
- [ ] `FindReferencingObjectsAsync(schema, name)` — reverse dependency lookup
- [ ] `GenerateDependencyGraphAsync(schema)` — full dependency tree

**QueryExecutor**
- [ ] `ExecuteQueryAsync(sql, parameters, timeout)` — read-only safe execution
- [ ] `ExecuteParameterizedQueryAsync(sql, parameters)` — Dapper parameter mapping
- [ ] `ValidateQueryAsync(sql)` — syntax + safety check without execution (`SET PARSEONLY ON`)
- [ ] `EstimateQueryCostAsync(sql)` — `SET SHOWPLAN_ALL ON`
- [ ] `GetExecutionPlanAsync(sql)` — `SET SHOWPLAN_XML ON`
- [ ] `AnalyzeQueryAsync(sql)` — SET STATISTICS IO/TIME combined analysis
- [ ] `ExecuteProcedureAsync(schema, name, parameters)` — safe SP execution

### Milestone 2.4 — Caching
- [ ] `MemoryCacheService` implementing `ICacheService`
  - [ ] Generic `GetOrSetAsync<T>` with TTL
  - [ ] Cache invalidation by key prefix
  - [ ] Cache statistics (hit/miss counters)
- [ ] `DistributedCacheService` implementing `ICacheService`
  - [ ] Redis-backed implementation
  - [ ] Serialization with `System.Text.Json`
  - [ ] Fallback to in-memory if Redis unavailable
- [ ] Cache keys:
  - [ ] `schemas:{database}` — TTL 1 hour
  - [ ] `tables:{database}:{schema}` — TTL 30 min
  - [ ] `describe:{schema}:{object}` — TTL 15 min
  - [ ] `definition:{schema}:{object}` — TTL 15 min

### Milestone 2.5 — Secrets Management
- [ ] `AzureKeyVaultSecretProvider` implementing `ISecretProvider`
  - [ ] Use `Azure.Identity.DefaultAzureCredential`
  - [ ] `GetSecretAsync(name)` with caching
- [ ] `EnvironmentVariableSecretProvider` implementing `ISecretProvider`
  - [ ] Read from environment variables
- [ ] `SecretProviderFactory` — selects provider based on config
- [ ] Never store secrets in appsettings.json (only references/names)

### Milestone 2.6 — Resilience (Polly)
- [ ] Define `ResiliencePipelineFactory`
  - [ ] Retry policy: 3 retries, exponential backoff (1s, 2s, 4s) on transient SQL errors
  - [ ] Circuit breaker: open after 5 failures in 30s, half-open after 60s
  - [ ] Timeout policy: configurable per tool (default 30s query timeout)
  - [ ] Bulkhead: limit concurrent DB operations
- [ ] Apply pipeline to `IQueryExecutor` and all repository calls

### Milestone 2.7 — Telemetry in Infrastructure
- [ ] OpenTelemetry `ActivitySource` for SQL operations
- [ ] Tag spans with: `db.system`, `db.name`, `db.statement` (sanitized), `db.operation`
- [ ] Instrument `SqlConnection` via `OpenTelemetry.Instrumentation.SqlClient`
- [ ] Custom metrics:
  - [ ] `sql.query.duration` histogram
  - [ ] `sql.connection.active` updown counter
  - [ ] `sql.cache.hit` / `sql.cache.miss` counters

### Milestone 2.8 — DI Registration
- [ ] `InfrastructureServiceExtensions.AddInfrastructure(IServiceCollection, IConfiguration)`
  - [ ] Register `IConnectionFactory` → `SqlConnectionFactory` (singleton)
  - [ ] Register all repositories (scoped)
  - [ ] Register `IQueryExecutor` (scoped)
  - [ ] Register `ICacheService` based on config (memory or Redis)
  - [ ] Register `ISecretProvider` based on config
  - [ ] Register Polly pipelines (singleton)

---

## Phase 3 — Application Layer (`SqlMcpServer.Application`)

### Milestone 3.1 — Project Scaffold
- [ ] `dotnet new classlib -n SqlMcpServer.Application`
- [ ] Reference `SqlMcpServer.Domain`
- [ ] Add NuGet: `FluentValidation`, `MediatR` (optional, for command/query separation)

### Milestone 3.2 — Configuration Models (Strongly Typed)
- [ ] `McpSettings` { ServerName, Version, Description, MaxConcurrentTools }
- [ ] `SqlServerSettings` { ConnectionString, AuthMode, Database, CommandTimeout, MaxPoolSize, MinPoolSize }
- [ ] `SecuritySettings` { AllowedRoles, EnableQuerySafetyValidation, DeniedKeywords[], AllowedStatements[], MaxResultRows, MaxQueryTimeoutSeconds }
- [ ] `TelemetrySettings` { ServiceName, OtlpEndpoint, EnableMetrics, EnableTracing, EnableLogging, SamplingRatio }
- [ ] `CacheSettings` { Provider (Memory/Redis), RedisConnectionString, DefaultTtlSeconds, SchemasTtlSeconds, DefinitionsTtlSeconds }
- [ ] `ToolSettings` { EnabledTools[], DefaultPageSize, MaxPageSize, SampleDataRowCount }

### Milestone 3.3 — Request/Response Models (DTOs)
Create request and response models for every tool. Examples:
- [ ] `GetTablesRequest` { Database, Schema, PageSize, PageNumber }
- [ ] `GetTablesResponse` { Tables: TableInfo[], TotalCount, Page, PageSize }
- [ ] `DescribeTableRequest` { Schema, TableName }
- [ ] `DescribeTableResponse` { Table: TableInfo, Columns: ColumnInfo[], PrimaryKeys, ForeignKeys, Indexes, Constraints, RowCount }
- [ ] `ExecuteQueryRequest` { Sql, Parameters: Dictionary<string,object>, TimeoutSeconds, MaxRows }
- [ ] `ExecuteQueryResponse` { Columns: string[], Rows: object[][], RowCount, ExecutionTimeMs, Truncated }
- [ ] `CompareSchemasRequest` { SourceDatabase, SourceSchema, TargetDatabase, TargetSchema }
- [ ] `CompareSchemasResponse` { Added: SchemaObjectName[], Removed: SchemaObjectName[], Modified: SchemaObjectName[], Script: string }
- [ ] `GenerateDocumentationRequest` { Database, Schema, IncludeTables, IncludeViews, IncludeProcedures, Format (Markdown/HTML/JSON) }
- [ ] (repeat for all 70+ tools)

### Milestone 3.4 — Validators (FluentValidation)
- [ ] `GetTablesRequestValidator` — schema name format, page size 1–500
- [ ] `ExecuteQueryRequestValidator` — not empty, max length 64KB, TimeoutSeconds 1–300
- [ ] `ExecuteProcedureRequestValidator` — schema + name not empty, parameter count limit
- [ ] `SampleTableDataRequestValidator` — row count 1–1000
- [ ] `CompareSchemasRequestValidator` — source != target
- [ ] (validator per request model where input validation needed)

### Milestone 3.5 — Query Safety Validator
- [ ] `QuerySafetyValidator` implementing `IQuerySafetyValidator`
  - [ ] Denylist check (case-insensitive regex):
    - [ ] `DROP\s+(DATABASE|TABLE|SCHEMA|INDEX|VIEW|PROCEDURE|FUNCTION|TRIGGER)`
    - [ ] `ALTER\s+LOGIN`
    - [ ] `SHUTDOWN`
    - [ ] `xp_cmdshell`
    - [ ] `EXEC\s*\(\s*@` (dynamic SQL via variable)
    - [ ] `sp_configure`
    - [ ] `OPENROWSET`, `OPENDATASOURCE` (linked server abuse)
    - [ ] `BULK\s+INSERT`
    - [ ] `RESTORE`, `BACKUP`
  - [ ] Allowlist mode (optional): only SELECT, WITH (CTEs), EXEC (safe SPs)
  - [ ] Statement type detection: SELECT vs DML vs DDL vs Admin
  - [ ] Role-based permission check: ReadOnly role can only run SELECT
  - [ ] Return `QuerySafetyResult { IsAllowed, ViolationReason, DetectedStatementType }`

### Milestone 3.6 — Business Services

**DatabaseDiscoveryService**
- [ ] `GetDatabasesAsync()` — with caching
- [ ] `GetSchemasAsync(database)` — with caching
- [ ] `GetTablesAsync(database, schema, page)` — paginated, cached
- [ ] `GetViewsAsync(database, schema)` — cached
- [ ] `GetFunctionsAsync(database, schema)` — cached
- [ ] `GetProceduresAsync(database, schema)` — cached
- [ ] `GetTriggersAsync(database, schema)` — cached
- [ ] `GetUserDefinedTypesAsync(schema)` — cached
- [ ] `GetTableTypesAsync(schema)` — cached
- [ ] `GetIndexesAsync(schema, table)` — cached
- [ ] `GetConstraintsAsync(schema, table)` — cached
- [ ] `GetForeignKeysAsync(schema, table)` — cached
- [ ] `GetSequencesAsync(schema)`
- [ ] `GetSynonymsAsync(schema)`

**TableService**
- [ ] `DescribeTableAsync(schema, table)` — full metadata aggregation
- [ ] `GetTableColumnsAsync(schema, table)`
- [ ] `GetPrimaryKeysAsync(schema, table)`
- [ ] `GetTableRelationshipsAsync(schema, table)`
- [ ] `GetTableStatisticsAsync(schema, table)`
- [ ] `GetRowCountAsync(schema, table)`
- [ ] `SampleTableDataAsync(schema, table, rows)` — validate row count limit
- [ ] `SearchTableDataAsync(schema, table, term)` — safety-validated

**ViewService, FunctionService, ProcedureService, TriggerService, TypeService**
- [ ] Mirror pattern of TableService with respective repository calls

**QueryService**
- [ ] `ExecuteQueryAsync(request)` — validate safety → execute → paginate
- [ ] `ExecuteParameterizedQueryAsync(request)` — Dapper parameter binding
- [ ] `ValidateQueryAsync(sql)` — SET PARSEONLY
- [ ] `EstimateQueryCostAsync(sql)` — SHOWPLAN_ALL
- [ ] `GetExecutionPlanAsync(sql)` — SHOWPLAN_XML
- [ ] `AnalyzeQueryAsync(sql)` — IO + TIME stats
- [ ] `FormatQueryAsync(sql)` — basic SQL formatting (indent, uppercase keywords)
- [ ] `ExecuteProcedureAsync(schema, name, params)` — safety-checked

**DependencyService**
- [ ] `FindObjectDependenciesAsync(schema, name)` — direct + transitive
- [ ] `FindReferencingObjectsAsync(schema, name)` — what uses this object
- [ ] `GenerateDependencyGraphAsync(schema)` — graph adjacency list
- [ ] `GenerateErdAsync(schema)` — tables + FK relationships in Mermaid/PlantUML format

**DocumentationService**
- [ ] `GenerateDatabaseDocumentationAsync(database, format)` — full DB doc
- [ ] `GenerateSchemaDocumentationAsync(schema, format)` — schema-level doc
- [ ] `GenerateTableDocumentationAsync(schema, table)` — single table doc
- [ ] `GenerateApiDocumentationAsync(database)` — procedure/function API doc
- [ ] Formats: Markdown, JSON, HTML

**SchemaComparisonService**
- [ ] `CompareSchemasAsync(source, target)` — diff tables, columns, indexes, constraints
- [ ] `CompareDatabasesAsync(sourceDb, targetDb)` — cross-database diff
- [ ] `GenerateMigrationScriptAsync(source, target)` — T-SQL ALTER/CREATE/DROP script

**HealthService**
- [ ] `HealthCheckAsync()` — overall health summary
- [ ] `DatabaseConnectivityTestAsync()` — test connection + simple query
- [ ] `CacheStatusAsync()` — cache provider health + stats
- [ ] `TelemetryStatusAsync()` — OTLP exporter status

### Milestone 3.7 — Audit Logging Service
- [ ] `AuditLogger` implementing `IAuditLogger`
  - [ ] `LogToolExecutionAsync(tool, user, parameters, result, duration)`
  - [ ] `LogQueryExecutionAsync(sql, user, database, rowCount, duration)`
  - [ ] `LogSecurityViolationAsync(user, tool, reason)`
  - [ ] Write to dedicated Serilog audit sink (separate from application logs)

### Milestone 3.8 — DI Registration
- [ ] `ApplicationServiceExtensions.AddApplication(IServiceCollection, IConfiguration)`
  - [ ] Bind all strongly-typed settings from `IConfiguration`
  - [ ] Register all services (scoped)
  - [ ] Register validators (via `FluentValidation.DependencyInjectionExtensions`)
  - [ ] Register `IQuerySafetyValidator` (singleton)
  - [ ] Register `IAuditLogger` (singleton)

---

## Phase 4 — Cross-Cutting Layer (`SqlMcpServer.CrossCutting`)

### Milestone 4.1 — Project Scaffold
- [ ] `dotnet new classlib -n SqlMcpServer.CrossCutting`
- [ ] Add NuGet: `Serilog`, `OpenTelemetry`, `Serilog.Sinks.OpenTelemetry`

### Milestone 4.2 — Logging Setup
- [ ] Configure Serilog in `LoggingExtensions.AddStructuredLogging()`
  - [ ] Console sink (human-readable in dev, JSON in prod)
  - [ ] File sink (rolling daily, 7-day retention)
  - [ ] Seq sink (if configured)
  - [ ] Enrichers: `WithCorrelationId`, `WithRequestId`, `WithMachineName`, `WithEnvironmentName`
  - [ ] Minimum level per environment (Debug/Information/Warning)
  - [ ] `Destructure.ToMaximumDepth(3)` to prevent log bloat

### Milestone 4.3 — Metrics Setup
- [ ] Configure OpenTelemetry in `TelemetryExtensions.AddTelemetry()`
  - [ ] MeterProvider with custom `SqlMcpServer` meter
  - [ ] Instruments:
    - [ ] `mcp.tool.duration` histogram (milliseconds, by tool name)
    - [ ] `mcp.tool.errors` counter (by tool name, error type)
    - [ ] `sql.query.duration` histogram (milliseconds, by operation type)
    - [ ] `cache.hits` / `cache.misses` counters
    - [ ] `sql.connections.active` updown counter
    - [ ] `mcp.requests.active` updown counter
  - [ ] OTLP exporter to configurable endpoint
  - [ ] Prometheus exporter (HTTP scrape endpoint)

### Milestone 4.4 — Tracing Setup
- [ ] TracerProvider with `SqlMcpServer` activity source
- [ ] Instrument: `OpenTelemetry.Instrumentation.SqlClient`
- [ ] Add `CorrelationId` to all spans
- [ ] Configure OTLP trace exporter
- [ ] Sampling: configurable ratio (default 0.1 in prod, 1.0 in dev)

### Milestone 4.5 — Correlation ID Middleware
- [ ] `CorrelationIdMiddleware` — read `X-Correlation-ID` header or generate new GUID
- [ ] Store in `IHttpContextAccessor` and `AsyncLocal<string>`
- [ ] Include in all log entries and span attributes

### Milestone 4.6 — Request Throttling
- [ ] Implement token bucket rate limiter
- [ ] Configure limits: max 100 requests/minute per client, max 10 concurrent
- [ ] Return structured error on throttle violation

---

## Phase 5 — Presentation Layer / MCP Server (`SqlMcpServer.Host`)

### Milestone 5.1 — Project Scaffold
- [ ] `dotnet new console -n SqlMcpServer.Host` (or worker service)
- [ ] Reference all other layers
- [ ] Add NuGet: `ModelContextProtocol`, `Microsoft.Extensions.Hosting`, `Serilog.Extensions.Hosting`

### Milestone 5.2 — Host Configuration
- [ ] `Program.cs` — build `IHostBuilder` with:
  - [ ] `AddApplication()`, `AddInfrastructure()`, cross-cutting registration
  - [ ] Configuration sources: `appsettings.json`, `appsettings.{env}.json`, environment variables, Azure Key Vault
  - [ ] Serilog as host logging provider
  - [ ] MCP server registration via `AddMcpServer()`
  - [ ] Graceful shutdown support

### Milestone 5.3 — MCP Transport
- [ ] Configure stdio transport for Claude Desktop integration
- [ ] Configure HTTP/SSE transport option for remote deployments
- [ ] Transport selection via config

### Milestone 5.4 — Tool Implementations

Each tool follows the pattern:
```
[McpServerTool, Name("tool_name"), Description("...")]
async Task<CallToolResponse> ToolNameAsync([Description("...")] string param, ...)
{
    // 1. Validate request (FluentValidation)
    // 2. Audit log entry
    // 3. Call service method (with cancellation token)
    // 4. Return structured JSON response
    // 5. On exception: return structured MCP error
}
```

**Database Discovery Tools (15 tools)**
- [ ] `get_databases` — list all accessible databases
- [ ] `get_schemas(database)` — list schemas
- [ ] `get_tables(database, schema, page, pageSize)` — paginated table list
- [ ] `get_views(database, schema)` — view list
- [ ] `get_functions(database, schema)` — all functions
- [ ] `get_procedures(database, schema)` — all stored procedures
- [ ] `get_triggers(database, schema)` — all triggers
- [ ] `get_user_defined_types(schema)` — UDTs
- [ ] `get_table_types(schema)` — table types
- [ ] `get_indexes(schema, table)` — indexes
- [ ] `get_constraints(schema, table)` — constraints
- [ ] `get_foreign_keys(schema, table)` — FKs
- [ ] `get_sequences(schema)` — sequences
- [ ] `get_synonyms(schema)` — synonyms

**Table Operation Tools (8 tools)**
- [ ] `describe_table(schema, table)` — full table metadata
- [ ] `get_table_columns(schema, table)` — columns
- [ ] `get_primary_keys(schema, table)` — PKs
- [ ] `get_table_relationships(schema, table)` — FK relationships
- [ ] `get_table_statistics(schema, table)` — size/row stats
- [ ] `get_row_count(schema, table)` — row count
- [ ] `sample_table_data(schema, table, rows)` — sample rows
- [ ] `search_table_data(schema, table, term, columns)` — search

**View Operation Tools (4 tools)**
- [ ] `describe_view(schema, view)`
- [ ] `get_view_definition(schema, view)`
- [ ] `get_view_dependencies(schema, view)`
- [ ] `get_view_columns(schema, view)`

**Function Operation Tools (5 tools)**
- [ ] `describe_function(schema, name)`
- [ ] `get_function_definition(schema, name)`
- [ ] `get_scalar_functions(schema)`
- [ ] `get_table_valued_functions(schema)`
- [ ] `analyze_function_dependencies(schema, name)`

**Stored Procedure Tools (5 tools)**
- [ ] `describe_procedure(schema, name)`
- [ ] `get_procedure_definition(schema, name)`
- [ ] `get_procedure_parameters(schema, name)`
- [ ] `execute_procedure(schema, name, parameters)` — role-gated
- [ ] `analyze_procedure_dependencies(schema, name)`

**Trigger Tools (3 tools)**
- [ ] `describe_trigger(schema, name)`
- [ ] `get_trigger_definition(schema, name)`
- [ ] `get_trigger_dependencies(schema, name)`

**Type Tools (3 tools)**
- [ ] `describe_user_defined_type(schema, name)`
- [ ] `describe_table_type(schema, name)`
- [ ] `get_type_definition(schema, name)`

**Query Tools (7 tools)**
- [ ] `execute_query(sql, parameters, timeout, maxRows)` — safety-validated
- [ ] `execute_parameterized_query(sql, parameters)` — Dapper binding
- [ ] `validate_query(sql)` — parse only
- [ ] `estimate_query_cost(sql)` — SHOWPLAN_ALL
- [ ] `get_execution_plan(sql)` — SHOWPLAN_XML
- [ ] `analyze_query(sql)` — IO + TIME
- [ ] `format_query(sql)` — format T-SQL

**Dependency Analysis Tools (4 tools)**
- [ ] `find_object_dependencies(schema, name)`
- [ ] `find_referencing_objects(schema, name)`
- [ ] `generate_dependency_graph(schema)`
- [ ] `generate_erd(schema)` — Mermaid diagram output

**Documentation Tools (4 tools)**
- [ ] `generate_database_documentation(database, format)`
- [ ] `generate_schema_documentation(schema, format)`
- [ ] `generate_table_documentation(schema, table)`
- [ ] `generate_api_documentation(database)`

**Schema Comparison Tools (3 tools)**
- [ ] `compare_schemas(sourceDb, sourceSchema, targetDb, targetSchema)`
- [ ] `compare_databases(sourceDb, targetDb)`
- [ ] `generate_migration_script(sourceDb, sourceSchema, targetDb, targetSchema)`

**Health Tools (4 tools)**
- [ ] `health_check`
- [ ] `database_connectivity_test`
- [ ] `cache_status`
- [ ] `telemetry_status`

### Milestone 5.5 — MCP Resources

Register MCP resources using `IResourceProvider`:
- [ ] `database://schemas` — list of all schemas
- [ ] `database://tables` — all tables with basic info
- [ ] `database://views` — all views
- [ ] `database://functions` — all functions
- [ ] `database://procedures` — all procedures
- [ ] `database://triggers` — all triggers
- [ ] `database://types` — UDTs and table types
- [ ] `database://indexes` — all indexes
- [ ] `database://relationships` — FK relationship map
- [ ] `database://documentation` — auto-generated DB docs
- [ ] `database://dependency-graph` — full dependency graph

### Milestone 5.6 — MCP Prompts

Register reusable prompt templates:
- [ ] `database-analysis` — "Analyze the {database} database structure and provide insights"
- [ ] `query-analysis` — "Analyze this SQL query for performance and correctness: {sql}"
- [ ] `schema-documentation` — "Generate comprehensive documentation for schema {schema}"
- [ ] `erd-generation` — "Generate an ERD diagram for schema {schema}"
- [ ] `dependency-analysis` — "Analyze all dependencies for object {schema}.{name}"
- [ ] `migration-generation` — "Generate migration script from {source} to {target}"
- [ ] `performance-review` — "Review database performance for schema {schema}"
- [ ] `security-review` — "Review security posture for database {database}"

### Milestone 5.7 — Error Handling Middleware
- [ ] Global MCP error handler — catch all exceptions → structured `McpError` response
- [ ] Map domain exceptions to MCP error codes:
  - [ ] `ValidationException` → code `-32602` (Invalid Params)
  - [ ] `SecurityException` → code `-32603` (Internal Error, obfuscated reason)
  - [ ] `QuerySafetyViolationException` → code `-32600` with safe message
  - [ ] `TransientException` → retry hint in response
  - [ ] `ObjectNotFoundException` → descriptive message
- [ ] Never expose stack traces or internal connection strings in errors

---

## Phase 6 — Security Layer

### Milestone 6.1 — RBAC Implementation
- [ ] `IUserContextProvider` — get current user role from config/environment
- [ ] `RbacAuthorizer` — check role permission per tool:
  - [ ] `ReadOnly`: get_*, describe_*, validate_query, health tools
  - [ ] `Developer`: ReadOnly + execute_query, format_query, analyze_query
  - [ ] `DBA`: Developer + execute_procedure, schema comparison, documentation
  - [ ] `Admin`: DBA + administrative operations
  - [ ] `Auditor`: get_*, describe_*, audit log access
- [ ] Apply role check as attribute/middleware on every tool handler

### Milestone 6.2 — Connection Security
- [ ] Enforce TLS for SQL Server connections (`Encrypt=true; TrustServerCertificate=false`)
- [ ] Certificate validation for production connections
- [ ] Connection string never logged (mask in Serilog destructuring policy)

### Milestone 6.3 — Query Row Limit Enforcement
- [ ] Inject `TOP {maxRows}` or add `OFFSET 0 ROWS FETCH NEXT {maxRows} ROWS ONLY` for safety
- [ ] Enforce configurable max (default 10,000 rows)
- [ ] Flag truncated results in response

### Milestone 6.4 — Input Sanitization
- [ ] Schema and object name validation — only alphanumeric, `_`, no SQL metacharacters
- [ ] Use `QUOTENAME()` for all dynamic object name references in Dapper queries
- [ ] Parameter binding only — never string concatenation for user input

---

## Phase 7 — Configuration Files

### Milestone 7.1 — appsettings.json Structure
```
{
  "Mcp": { ServerName, Version, MaxConcurrentTools },
  "SqlServer": { AuthMode, Database, CommandTimeout, MaxPoolSize },
  "Security": { EnableQuerySafety, MaxResultRows, MaxQueryTimeout },
  "Telemetry": { ServiceName, OtlpEndpoint, EnableMetrics, EnableTracing },
  "Cache": { Provider, DefaultTtlSeconds },
  "Tools": { DefaultPageSize, MaxPageSize, SampleDataRowCount }
}
```
- [ ] `appsettings.Development.json` — relaxed limits, verbose logging, in-memory cache
- [ ] `appsettings.Production.json` — strict limits, info logging, Redis cache, OTLP enabled

### Milestone 7.2 — Secrets Configuration
- [ ] Connection string referenced via `SqlServer__ConnectionString` environment variable
- [ ] Azure Key Vault integration: `KeyVault__Uri` → auto-load secrets at startup
- [ ] `dotnet user-secrets` for local development (never commit to source)

---

## Phase 8 — Containerization

### Milestone 8.1 — Dockerfile
```
Stage 1 (build): mcr.microsoft.com/dotnet/sdk:8.0
  - Restore, build, publish to /app
Stage 2 (runtime): mcr.microsoft.com/dotnet/aspnet:8.0
  - Copy from build stage
  - Run as non-root user
  - EXPOSE 8080
  - ENTRYPOINT ["dotnet", "SqlMcpServer.Host.dll"]
```
- [ ] Multi-stage build
- [ ] Non-root user (`app` user, UID 1000)
- [ ] Health check instruction
- [ ] `.dockerignore` to exclude `bin/`, `obj/`, `*.user`, secrets files

### Milestone 8.2 — Docker Compose
- [ ] `docker-compose.yml`:
  - [ ] `mcp-server` service (built from Dockerfile)
  - [ ] `sqlserver` service (`mcr.microsoft.com/mssql/server:2022-latest`)
  - [ ] `redis` service (for distributed cache)
  - [ ] `seq` service (for structured log viewing in dev)
  - [ ] `otel-collector` service (OpenTelemetry Collector)
  - [ ] Named volumes for SQL Server data, Seq data
  - [ ] Environment variables for secrets (loaded from `.env` file)
- [ ] `docker-compose.override.yml` for dev overrides

### Milestone 8.3 — Kubernetes Manifests (`deploy/kubernetes/`)
- [ ] `namespace.yaml`
- [ ] `configmap.yaml` — non-secret configuration
- [ ] `secret.yaml` (template only — real values via Sealed Secrets or External Secrets)
- [ ] `deployment.yaml`:
  - [ ] 2 replicas minimum
  - [ ] Resource requests/limits (CPU: 250m/1000m, Memory: 256Mi/512Mi)
  - [ ] Liveness probe: `health_check` tool or HTTP /health endpoint
  - [ ] Readiness probe: database connectivity test
  - [ ] Rolling update strategy
- [ ] `service.yaml` — ClusterIP for HTTP transport
- [ ] `hpa.yaml` — HorizontalPodAutoscaler (min 2, max 10, CPU 70%)
- [ ] `serviceaccount.yaml` — for Azure Workload Identity

---

## Phase 9 — Claude Desktop Integration

### Milestone 9.1 — Windows Setup
```json
// %APPDATA%\Claude\claude_desktop_config.json
{
  "mcpServers": {
    "mssql": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\SqlMcpServer.Host"],
      "env": {
        "SqlServer__ConnectionString": "Server=.;Database=MyDb;Trusted_Connection=true"
      }
    }
  }
}
```
- [ ] Document Windows setup steps
- [ ] PowerShell install script

### Milestone 9.2 — Linux Setup
- [ ] Shell script for Linux setup
- [ ] Config path: `~/.config/Claude/claude_desktop_config.json`
- [ ] Bash install script

### Milestone 9.3 — Docker Setup for Claude Desktop
```json
{
  "mcpServers": {
    "mssql": {
      "command": "docker",
      "args": ["run", "--rm", "-i", "-e", "SqlServer__ConnectionString=...",
               "sqlmcpserver:latest"]
    }
  }
}
```

### Milestone 9.4 — Published Binary Setup
- [ ] `dotnet publish -c Release -r win-x64 --self-contained`
- [ ] Point Claude Desktop at the `.exe` directly (no dotnet runtime needed)

---

## Phase 10 — CI/CD

### Milestone 10.1 — GitHub Actions (`.github/workflows/`)

**`ci.yml`** — runs on PR and push to main:
- [ ] Checkout → Setup .NET 8 → Restore → Build → Security Scan → Docker Build
- [ ] Upload build artifacts

**`security-scan.yml`**:
- [ ] OWASP Dependency Check (NuGet CVE scanning)
- [ ] `dotnet-security-scan` or Snyk
- [ ] Trivy container scan on built Docker image

**`cd.yml`** — runs on merge to main:
- [ ] Build → Publish Docker image to GHCR or ACR
- [ ] Version tag from `git tag` or semantic versioning
- [ ] Deploy to staging environment
- [ ] Manual approval gate → deploy to production

**`release.yml`**:
- [ ] Auto-generate release notes from conventional commits
- [ ] Create GitHub Release with changelog

### Milestone 10.2 — Azure DevOps Pipeline (`deploy/azure-devops/`)
- [ ] `azure-pipelines.yml` — equivalent of GitHub Actions CI/CD
- [ ] Service connection for ACR, AKS, Azure Key Vault
- [ ] Pipeline variable groups (dev/staging/prod) for secrets

---

## Phase 11 — Documentation (`docs/`)

### Milestone 11.1 — README.md
- [ ] Project overview and purpose
- [ ] Prerequisites (runtime, SQL Server version, .NET 8)
- [ ] Quick Start (5 commands to run)
- [ ] Claude Desktop integration steps
- [ ] Configuration reference table
- [ ] Tool catalog with descriptions
- [ ] License

### Milestone 11.2 — Architecture Document (`docs/architecture/`)
- [ ] Solution layer diagram
- [ ] Data flow: Claude → MCP Server → Repository → SQL Server
- [ ] Security model diagram
- [ ] Caching strategy diagram
- [ ] Deployment topology diagram

### Milestone 11.3 — Deployment Guide (`docs/deployment/`)
- [ ] Windows bare-metal deployment
- [ ] Linux bare-metal deployment
- [ ] Docker single-node deployment
- [ ] Docker Compose deployment
- [ ] Kubernetes deployment (step-by-step)
- [ ] Azure Container Apps deployment
- [ ] Azure Kubernetes Service deployment
- [ ] Configuration for each environment

### Milestone 11.4 — Operations Guide (`docs/operations/`)
- [ ] Starting and stopping the server
- [ ] Log locations and log level adjustment
- [ ] Metrics endpoints and Grafana dashboard setup
- [ ] Cache clearing procedures
- [ ] Adding new SQL Server connections
- [ ] Role management
- [ ] Performance tuning knobs
- [ ] Backup and recovery of server configuration

### Milestone 11.5 — Troubleshooting Guide (`docs/troubleshooting/`)
- [ ] Connection refused → check SQL Server, connection string, firewall
- [ ] Authentication failed → auth mode config, Azure AD token, Windows identity
- [ ] Query timeout → CommandTimeout setting, query optimization tips
- [ ] Tool not found in Claude → MCP server not started, config path wrong
- [ ] Circuit breaker open → SQL Server health, check logs
- [ ] High memory → large result set protection, reduce MaxResultRows
- [ ] Certificate errors → TrustServerCertificate setting, cert thumbprint

---

## Execution Order (Team Checklist)

Execute phases in this order to minimize rework:

```
Week 1:  Phase 0 (Setup) → Phase 1 (Domain)
Week 2:  Phase 2 (Infrastructure — connection + repositories)
Week 3:  Phase 2 cont. (caching, resilience) → Phase 3 (Application services)
Week 4:  Phase 3 cont. (remaining services) → Phase 4 (Cross-cutting)
Week 5:  Phase 5 (MCP Host — tools, resources, prompts)
Week 6:  Phase 6 (Security) → Phase 7 (Configuration)
Week 7:  Phase 8 (Docker + Kubernetes)
Week 8:  Phase 9 (Claude Desktop) → Phase 10 (CI/CD)
Week 9:  Phase 11 (Documentation) → End-to-end validation
```

---

## Dependencies Map

```
Domain          ← no dependencies (pure)
CrossCutting    ← Domain
Infrastructure  ← Domain, CrossCutting
Application     ← Domain, CrossCutting
Host            ← Application, Infrastructure, CrossCutting
```

---

## Key Risk Items

| Risk | Mitigation |
|---|---|
| MCP SDK API changes | Pin exact SDK version in `Directory.Packages.props` |
| SQL Server version compatibility | Test against 2019 and 2022; use INFORMATION_SCHEMA for portability |
| Azure AD token expiry | Use `DefaultAzureCredential` which handles token refresh automatically |
| Large result sets crashing server | Enforce row limits + streaming with `IAsyncEnumerable` |
| Query safety bypass via encoding | Normalize SQL before denylist check; use TSql100Parser if needed |
| Secrets leaking in logs | Custom Serilog destructuring policy masking connection strings |
| Circuit breaker blocking valid traffic | Configure half-open probe, expose `cache_status` tool for ops team |
