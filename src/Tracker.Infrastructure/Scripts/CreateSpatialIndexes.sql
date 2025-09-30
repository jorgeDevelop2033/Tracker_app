IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Porticos_Ubicacion')
    CREATE SPATIAL INDEX IX_Porticos_Ubicacion ON dbo.Porticos(Ubicacion);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Porticos_Corredor')
    CREATE SPATIAL INDEX IX_Porticos_Corredor ON dbo.Porticos(Corredor);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Transitos_Posicion')
    CREATE SPATIAL INDEX IX_Transitos_Posicion ON dbo.Transitos(Posicion);
