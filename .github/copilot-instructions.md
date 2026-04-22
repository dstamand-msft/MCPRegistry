This repository implements a self-hosted MCP (Model Context Protocol) Server Registry API with a Blazor Server management UI, deployed to Azure Container Apps with Azure SQL Database.

## Project Structure

- `src/MCPRegistry/` — ASP.NET Core 10 REST API (MCP Registry v0.1 spec)
- `src/MCPRegistry.UI/` — Blazor Server management UI
- `src/MCPRegistryDatabase/` — SQL Database Project (SDK-style, produces dacpac)
- `src/MCPRegistry.Tests/` — API unit tests (xUnit + Moq + FluentAssertions)
- `src/MCPRegistry.UI.Tests/` — UI unit tests (xUnit + Moq + FluentAssertions)
- `azd/` — Azure Developer CLI deployment (Bicep with AVM modules)
- `data/` — Sample MCP server definitions for seeding

## Key Conventions

- Follow the instruction files in `.github/instructions/` for C#, REST API, security, and commenting standards.
- Use the CSharpExpert agent (`.github/agents/`) for .NET-specific guidance.
- All code must build with `dotnet build` from the repo root — no Visual Studio or SSDT dependency.
- Database schema is managed via the SQL project dacpac — do not write migration scripts manually.
- Server metadata is immutable per MCP spec — no PUT/PATCH endpoints. New versions are added via POST, status changes via DELETE (soft-delete).
- Infrastructure uses Azure Verified Modules (AVM) — prefer AVM over raw ARM resources.
- Resource names follow Azure CAF naming conventions with customer-overridable parameters.
- No hardcoded secrets — use Managed Identity and Entra-only auth.
- Run `dotnet test` before committing to verify all tests pass.
