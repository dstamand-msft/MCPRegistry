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

Example DSP values (tooling dependent):
- `Microsoft.Data.Tools.Schema.Sql.SqlServer2019DatabaseSchemaProvider`
- `Microsoft.Data.Tools.Schema.Sql.SqlServer2017DatabaseSchemaProvider`
- `Microsoft.Data.Tools.Schema.Sql.SqlAzureV12DatabaseSchemaProvider`

Refer to the link above for the authoritative list and any newer platform identifiers.

## CI Recommendations

- Build the `.sqlproj` in CI to produce a deterministic `.dacpac` artifact.
- Use MSBuild properties (`/p:...`) to parameterize `DSP`, `PackageVersion`, and output paths in your pipeline.
- Run `sqlpackage` from CI only when you need to publish or extract a `.dacpac`.
