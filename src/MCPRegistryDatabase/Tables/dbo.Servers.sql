CREATE TABLE Servers (
    ServerName NVARCHAR(255) NOT NULL,
    [Version] NVARCHAR(255) NOT NULL,
    [Status] NVARCHAR(50) NOT NULL DEFAULT 'active',
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    IsLatest BIT NOT NULL DEFAULT 1,
    [Value] NVARCHAR(MAX) NULL, -- JSON as text for extra data
    CONSTRAINT PK_Servers PRIMARY KEY (ServerName, Version),
    CONSTRAINT CHK_Servers_Status CHECK (Status IN ('active', 'deprecated', 'deleted')),
    CONSTRAINT CHK_Servers_ServerNameFormat CHECK (ServerName LIKE '[a-zA-Z0-9]%/[a-zA-Z0-9]%'),
    CONSTRAINT CHK_Servers_VersionNotEmpty CHECK (LEN(LTRIM(RTRIM(Version))) > 0),
    CONSTRAINT CHK_Servers_CreatedAtReasonable CHECK (CreatedAt >= '2020-01-01' AND CreatedAt <= DATEADD(DAY, 1, SYSDATETIMEOFFSET()))
);

GO;

-- Indexes
CREATE INDEX IDX_Servers_ServerName ON Servers(ServerName);
GO;

CREATE INDEX IDX_Servers_ServerNameVersion ON Servers(ServerName, [Version]);
GO;

CREATE INDEX IDX_Servers_ServerNameLatest ON Servers(ServerName, IsLatest) WHERE IsLatest = 1;
GO;

CREATE INDEX IDX_Servers_Status ON Servers([Status]);
GO;

CREATE INDEX IDX_Servers_CreatedAt ON Servers(CreatedAt DESC);
GO;

CREATE INDEX IDX_Servers_UpdatedAt ON Servers(UpdatedAt DESC);
GO;

CREATE UNIQUE INDEX IDX_UniqueLatestPerServer ON Servers(ServerName) WHERE IsLatest = 1;
GO;
