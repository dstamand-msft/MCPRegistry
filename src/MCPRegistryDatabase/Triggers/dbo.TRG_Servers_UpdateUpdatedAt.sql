CREATE TRIGGER TRG_Servers_UpdateUpdatedAt
ON Servers
AFTER UPDATE
AS
BEGIN
    UPDATE Servers
    SET UpdatedAt = SYSDATETIMEOFFSET()
    FROM Inserted
    WHERE Servers.ServerName = Inserted.ServerName AND Servers.Version = Inserted.Version;
END;