# MCPRegistry Database - Build & Deploy

This project is a SQL Database project (`.sqlproj`). The instructions below show how to build the project to produce a `.dacpac`, how to change the target platform using the `DSP` MSBuild property, and how to deploy the dacpac with `sqlpackage`.

## Build (produce a `.dacpac`)

Using `dotnet build` (cross-platform):

```sh
dotnet build MCPRegistryDatabase.sqlproj -c Release -o ./artifacts /p:DSP="Microsoft.Data.Tools.Schema.Sql.SqlServer2019DatabaseSchemaProvider" /p:PackageVersion=1.0.0
```

Using `msbuild`:

```sh
msbuild MCPRegistryDatabase.sqlproj /t:Build /p:Configuration=Release /p:OutDir=.\\artifacts\\ /p:DSP="Microsoft.Data.Tools.Schema.Sql.SqlServer2019DatabaseSchemaProvider" /p:PackageVersion=1.0.0
```

Notes:
- `DSP` is the MSBuild property that controls the Target Platform for the SQL project. You can set it inside the `.sqlproj` or pass it on the CLI with `/p:DSP=...`.
- `PackageVersion` sets the version metadata included in the produced `.dacpac`.
- `OutDir` / `-o` controls where the `.dacpac` will be written.

Example `.sqlproj` snippet (set values in the project):

```xml
<PropertyGroup>
  <DSP>Microsoft.Data.Tools.Schema.Sql.SqlServer2019DatabaseSchemaProvider</DSP>
  <PackageVersion>1.0.0</PackageVersion>
</PropertyGroup>
```

## Deploy / Publish

Use `sqlpackage` to publish a `.dacpac` to a target server or to extract a `.dacpac` from a live database.

Extract from a live database:

```sh
sqlpackage /Action:Extract /SourceServerName:"<server>" /SourceDatabaseName:"<db>" /TargetFile:./artifacts/<db>.dacpac
```

Publish a dacpac to a target database:

```sh
sqlpackage /Action:Publish /SourceFile:./artifacts/<db>.dacpac /TargetServerName:"<server>" /TargetDatabaseName:"<targetDb>"
```

## Changing the Target Platform (DSP)

The `DSP` MSBuild property selects the database platform schema used when building the project (for example: SQL Server 2019, Azure V12, etc.). Set `DSP` by editing the `.sqlproj` or by passing `/p:DSP=...` on the `dotnet build` / `msbuild` command line.

For the full list of supported target platform identifiers and guidance, see the official Microsoft documentation:

https://learn.microsoft.com/en-us/sql/tools/sql-database-projects/concepts/target-platform?view=sql-server-ver17&pivots=sq1-command-line

Current DSP values documented by Microsoft:

| DSP value | Target platform |
|---|---|
| `Microsoft.Data.Tools.Schema.Sql.Sql120DatabaseSchemaProvider` | SQL Server 2014 |
| `Microsoft.Data.Tools.Schema.Sql.Sql130DatabaseSchemaProvider` | SQL Server 2016 |
| `Microsoft.Data.Tools.Schema.Sql.Sql140DatabaseSchemaProvider` | SQL Server 2017 |
| `Microsoft.Data.Tools.Schema.Sql.Sql150DatabaseSchemaProvider` | SQL Server 2019 |
| `Microsoft.Data.Tools.Schema.Sql.Sql160DatabaseSchemaProvider` | SQL Server 2022 |
| `Microsoft.Data.Tools.Schema.Sql.SqlAzureV12DatabaseSchemaProvider` | Azure SQL Database |
| `Microsoft.Data.Tools.Schema.Sql.SqlDbFabricDatabaseSchemaProvider` | SQL database in Fabric / Fabric Mirrored SQL Database (preview) |
| `Microsoft.Data.Tools.Schema.Sql.SqlDwDatabaseSchemaProvider` | Azure Synapse SQL Pool |
| `Microsoft.Data.Tools.Schema.Sql.SqlServerlessDatabaseSchemaProvider` | Azure Synapse Serverless SQL Pool |
| `Microsoft.Data.Tools.Schema.Sql.SqlDwUnifiedDatabaseSchemaProvider` | Fabric Data Warehouse |

Note: this list can change as new tooling and platform versions are released. Always refer to the official Microsoft documentation link above for the latest supported values.

## CI Recommendations

- Build the `.sqlproj` in CI to produce a deterministic `.dacpac` artifact.
- Use MSBuild properties (`/p:...`) to parameterize `DSP`, `PackageVersion`, and output paths in your pipeline.
- Run `sqlpackage` from CI only when you need to publish or extract a `.dacpac`.
