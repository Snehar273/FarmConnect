-- Created automatically at startup by DbSeeder (CREATE OR ALTER). Kept here for reference.
CREATE OR ALTER VIEW dbo.vw_PriceHistory AS
SELECT ph.PriceHistoryId, ph.ProduceId, p.Name AS ProduceName, ph.MarketName, ph.PriceDate,
       ph.MinPrice, ph.MaxPrice, ph.ModalPrice, ph.Unit, ph.Source
FROM dbo.PriceHistory ph
INNER JOIN dbo.Produce p ON ph.ProduceId = p.ProduceId;
